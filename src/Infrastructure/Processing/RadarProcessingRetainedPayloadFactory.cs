using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingRetainedPayloadFactory
{
    private readonly ArrayPool<RadarStreamEvent> eventPool;
    private readonly ArrayPool<byte> payloadPool;

    public RadarProcessingRetainedPayloadFactory()
        : this(ArrayPool<RadarStreamEvent>.Shared, ArrayPool<byte>.Shared)
    {
    }

    public RadarProcessingRetainedPayloadFactory(
        ArrayPool<RadarStreamEvent> eventPool,
        ArrayPool<byte> payloadPool)
    {
        this.eventPool = eventPool ?? throw new ArgumentNullException(nameof(eventPool));
        this.payloadPool = payloadPool ?? throw new ArgumentNullException(nameof(payloadPool));
    }

    public RadarProcessingRetainedPayloadRetentionResult Retain(
        RadarEventBatch batch,
        RadarProcessingRetainedPayloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var effectiveOptions = options ?? RadarProcessingRetainedPayloadOptions.Default;

        if (cancellationToken.IsCancellationRequested)
        {
            return RadarProcessingRetainedPayloadRetentionResult.Canceled(
                effectiveOptions.Strategy,
                "Retained payload retention was canceled before it started.");
        }

        return effectiveOptions.Strategy switch
        {
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy =>
                RetainSnapshotCopy(batch, cancellationToken),
            RadarProcessingRetainedPayloadStrategy.PooledCopy =>
                RetainPooledCopy(batch, cancellationToken),
            RadarProcessingRetainedPayloadStrategy.BuilderTransfer =>
                RadarProcessingRetainedPayloadRetentionResult.UnsupportedStrategy(
                    effectiveOptions.Strategy,
                    "Builder transfer retention is not implemented in this milestone slice."),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
    }

    private static RadarProcessingRetainedPayloadRetentionResult RetainSnapshotCopy(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return RadarProcessingRetainedPayloadRetentionResult.Canceled(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                "Retained payload retention was canceled before snapshot copy.");
        }

        var before = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var started = TimeProvider.System.GetTimestamp();
        var owned = batch.ToOwnedSnapshot();
        var elapsed = TimeProvider.System.GetElapsedTime(started);
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(before);

        return RadarProcessingRetainedPayloadRetentionResult.Succeeded(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            owned,
            RadarProcessingRetainedBatchResource.NotRequired(RadarProcessingRetainedPayloadStrategy.SnapshotCopy),
            elapsed,
            allocatedBytes);
    }

    private RadarProcessingRetainedPayloadRetentionResult RetainPooledCopy(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        if (batch.Lifetime == RadarEventBatchLifetime.Owned)
        {
            return RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                batch,
                RadarProcessingRetainedBatchResource.NotRequired(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return RadarProcessingRetainedPayloadRetentionResult.Canceled(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                "Retained payload retention was canceled before pooled copy.");
        }

        RadarStreamEvent[]? eventArray = null;
        byte[]? payloadArray = null;
        try
        {
            var before = RadarProcessingBenchmarkAllocationSnapshot.Capture();
            var started = TimeProvider.System.GetTimestamp();

            ReadOnlyMemory<RadarStreamEvent> events = ReadOnlyMemory<RadarStreamEvent>.Empty;
            if (batch.EventCount > 0)
            {
                eventArray = eventPool.Rent(batch.EventCount);
                batch.Events.Span.CopyTo(eventArray.AsSpan(0, batch.EventCount));
                events = eventArray.AsMemory(0, batch.EventCount);
            }

            ReadOnlyMemory<byte> payload = ReadOnlyMemory<byte>.Empty;
            if (batch.PayloadLength > 0)
            {
                payloadArray = payloadPool.Rent(batch.PayloadLength);
                batch.Payload.Span.CopyTo(payloadArray.AsSpan(0, batch.PayloadLength));
                payload = payloadArray.AsMemory(0, batch.PayloadLength);
            }

            RadarEventBatch retained;
            if (batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum))
            {
                retained = new RadarEventBatch(
                    batch.StreamSchemaVersion,
                    batch.DictionaryVersion,
                    batch.SourceUniverseVersion,
                    events,
                    payload,
                    payloadValueCount,
                    rawValueChecksum);
            }
            else
            {
                retained = new RadarEventBatch(
                    batch.StreamSchemaVersion,
                    batch.DictionaryVersion,
                    batch.SourceUniverseVersion,
                    events,
                    payload);
            }

            var capturedEventArray = eventArray;
            var capturedPayloadArray = payloadArray;
            var resource = capturedEventArray is null && capturedPayloadArray is null
                ? RadarProcessingRetainedBatchResource.NotRequired(RadarProcessingRetainedPayloadStrategy.PooledCopy)
                : new RadarProcessingRetainedBatchResource(
                    RadarProcessingRetainedPayloadStrategy.PooledCopy,
                    batch.PayloadLength,
                    () =>
                    {
                        if (capturedEventArray is not null)
                        {
                            eventPool.Return(capturedEventArray);
                        }

                        if (capturedPayloadArray is not null)
                        {
                            payloadPool.Return(capturedPayloadArray);
                        }

                        return RadarProcessingRetainedPayloadReleaseResult.Released(
                            RadarProcessingRetainedPayloadStrategy.PooledCopy,
                            payloadBytes: batch.PayloadLength);
                    });

            var elapsed = TimeProvider.System.GetElapsedTime(started);
            var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(before);
            eventArray = null;
            payloadArray = null;

            return RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                retained,
                resource,
                elapsed,
                allocatedBytes);
        }
        catch (Exception exception)
        {
            if (eventArray is not null)
            {
                eventPool.Return(eventArray);
            }

            if (payloadArray is not null)
            {
                payloadPool.Return(payloadArray);
            }

            return RadarProcessingRetainedPayloadRetentionResult.FailedCopy(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                exception.Message);
        }
    }
}
