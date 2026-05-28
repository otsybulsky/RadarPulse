using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingOwnedBatchQueue
{
    /// <summary>
    /// Enqueues an owned batch, optionally waiting according to the configured full mode.
    /// </summary>
    /// <remarks>
    /// The method rejects non-owned batches because queued providers retain input
    /// beyond the caller's immediate stack frame. Accepted batches receive the
    /// next provider sequence and are included in retained-resource telemetry.
    /// </remarks>
    /// <returns>
    /// An accepted result with the queued batch, or a full, timed-out, canceled,
    /// closed, or faulted result that describes why the batch was not accepted.
    /// </returns>
    public ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default,
        Action<RadarProcessingQueuedBatch>? onAccepted = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Lifetime != RadarEventBatchLifetime.Owned)
        {
            throw new ArgumentException("Owned batch queue accepts only owned RadarEventBatch values.", nameof(batch));
        }

        if (ownedSnapshotTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ownedSnapshotTime));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);

        var started = Stopwatch.GetTimestamp();
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                    Stopwatch.GetElapsedTime(started)));
        }

        var stateRejection = TryGetStateRejection();
        if (stateRejection.HasValue)
        {
            return new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                RecordRejected(
                    stateRejection.Value.Status,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection.Value.Message));
        }

        var oversizedRejection = TryCreateOversizedRetainedByteBudgetMessage(batch.PayloadLength);
        if (oversizedRejection is not null)
        {
            return new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.Full,
                    Stopwatch.GetElapsedTime(started),
                    oversizedRejection));
        }

        return Options.FullMode == RadarProcessingProviderQueueFullMode.ReturnFull
            ? new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                TryEnqueueWithoutWaiting(batch, ownedSnapshotTime, ownedSnapshotAllocatedBytes, started, onAccepted))
            : EnqueueWithWaitAsync(
                  batch,
                  ownedSnapshotTime,
                  ownedSnapshotAllocatedBytes,
                  started,
                  cancellationToken,
                  onAccepted);
    }

    private RadarProcessingQueuedBatchEnqueueResult TryEnqueueWithoutWaiting(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime,
        long allocatedBytes,
        long started,
        Action<RadarProcessingQueuedBatch>? onAccepted)
    {
        lock (sync)
        {
            var stateRejection = TryGetStateRejectionUnsafe();
            if (stateRejection.HasValue)
            {
                return RecordRejectedUnsafe(
                    stateRejection.Value.Status,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection.Value.Message);
            }

            var queuedBatch = CreateQueuedBatchUnsafe(batch, ownedSnapshotTime, allocatedBytes);
            if (!HasRetainedByteCapacityUnsafe(queuedBatch.PayloadBytes))
            {
                return RecordRejectedUnsafe(
                    RadarProcessingQueuedBatchEnqueueStatus.Full,
                    Stopwatch.GetElapsedTime(started),
                    CreateRetainedByteBudgetExhaustedMessageUnsafe(queuedBatch.PayloadBytes));
            }

            if (!channel.Writer.TryWrite(queuedBatch))
            {
                stateRejection = TryGetStateRejectionUnsafe();
                if (stateRejection.HasValue)
                {
                    return RecordRejectedUnsafe(
                        stateRejection.Value.Status,
                        Stopwatch.GetElapsedTime(started),
                        stateRejection.Value.Message);
                }

                return RecordRejectedUnsafe(
                    RadarProcessingQueuedBatchEnqueueStatus.Full,
                    Stopwatch.GetElapsedTime(started));
            }

            var accepted = RecordAcceptedUnsafe(queuedBatch, Stopwatch.GetElapsedTime(started));
            onAccepted?.Invoke(queuedBatch);
            return accepted;
        }
    }

    private async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueWithWaitAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime,
        long allocatedBytes,
        long started,
        CancellationToken cancellationToken,
        Action<RadarProcessingQueuedBatch>? onAccepted)
    {
        using var timeout = Options.EnqueueTimeout.HasValue
            ? new CancellationTokenSource(Options.EnqueueTimeout.Value)
            : null;
        using var linked = timeout is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var waitToken = linked?.Token ?? cancellationToken;

        while (true)
        {
            var stateRejection = TryGetStateRejection();
            if (stateRejection.HasValue)
            {
                return RecordRejected(
                    stateRejection.Value.Status,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection.Value.Message);
            }

            bool canWrite;
            try
            {
                canWrite = await channel.Writer.WaitToWriteAsync(waitToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                    Stopwatch.GetElapsedTime(started));
            }
            catch (OperationCanceledException) when (timeout?.IsCancellationRequested == true)
            {
                return RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.TimedOut,
                    Stopwatch.GetElapsedTime(started));
            }

            if (!canWrite)
            {
                stateRejection = TryGetStateRejection();
                return RecordRejected(
                    stateRejection?.Status ?? RadarProcessingQueuedBatchEnqueueStatus.Closed,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection?.Message ?? string.Empty);
            }

            Task? retainedByteBudgetWait = null;
            lock (sync)
            {
                stateRejection = TryGetStateRejectionUnsafe();
                if (stateRejection.HasValue)
                {
                    return RecordRejectedUnsafe(
                        stateRejection.Value.Status,
                        Stopwatch.GetElapsedTime(started),
                        stateRejection.Value.Message);
                }

                var queuedBatch = CreateQueuedBatchUnsafe(batch, ownedSnapshotTime, allocatedBytes);
                if (!HasRetainedByteCapacityUnsafe(queuedBatch.PayloadBytes))
                {
                    retainedByteBudgetWait = retainedByteBudgetChanged.Task;
                }
                else if (channel.Writer.TryWrite(queuedBatch))
                {
                    var accepted = RecordAcceptedUnsafe(queuedBatch, Stopwatch.GetElapsedTime(started));
                    onAccepted?.Invoke(queuedBatch);
                    return accepted;
                }
            }

            if (retainedByteBudgetWait is not null)
            {
                try
                {
                    await retainedByteBudgetWait.WaitAsync(waitToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return RecordRejected(
                        RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                        Stopwatch.GetElapsedTime(started));
                }
                catch (OperationCanceledException) when (timeout?.IsCancellationRequested == true)
                {
                    return RecordRejected(
                        RadarProcessingQueuedBatchEnqueueStatus.TimedOut,
                        Stopwatch.GetElapsedTime(started),
                        CreateRetainedByteBudgetTimedOutMessage(batch.PayloadLength));
                }

                continue;
            }
        }
    }


    private RadarProcessingQueuedBatchEnqueueResult RecordAcceptedUnsafe(
        RadarProcessingQueuedBatch batch,
        TimeSpan enqueueWaitTime)
    {
        nextSequence = checked(nextSequence + 1);
        pendingCount++;
        pendingPayloadBytes = checked(pendingPayloadBytes + batch.PayloadBytes);
        counters.RecordAccepted(batch, enqueueWaitTime, pendingCount, pendingPayloadBytes);

        var result = RadarProcessingQueuedBatchEnqueueResult.Accepted(batch, enqueueWaitTime);
        telemetryRecorder.RecordEnqueueResult(result, pendingCount, pendingPayloadBytes);
        return result;
    }

    private RadarProcessingQueuedBatchEnqueueResult RecordRejected(
        RadarProcessingQueuedBatchEnqueueStatus status,
        TimeSpan enqueueWaitTime,
        string message = "")
    {
        lock (sync)
        {
            return RecordRejectedUnsafe(status, enqueueWaitTime, message);
        }
    }

    private RadarProcessingQueuedBatchEnqueueResult RecordRejectedUnsafe(
        RadarProcessingQueuedBatchEnqueueStatus status,
        TimeSpan enqueueWaitTime,
        string message = "")
    {
        counters.RecordRejected(status, enqueueWaitTime);
        switch (status)
        {
            case RadarProcessingQueuedBatchEnqueueStatus.Full:
                var full = RadarProcessingQueuedBatchEnqueueResult.Full(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(full, pendingCount, pendingPayloadBytes);
                return full;

            case RadarProcessingQueuedBatchEnqueueStatus.TimedOut:
                var timedOut = RadarProcessingQueuedBatchEnqueueResult.TimedOut(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(timedOut, pendingCount, pendingPayloadBytes);
                return timedOut;

            case RadarProcessingQueuedBatchEnqueueStatus.Canceled:
                var canceled = RadarProcessingQueuedBatchEnqueueResult.Canceled(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(canceled, pendingCount, pendingPayloadBytes);
                return canceled;

            case RadarProcessingQueuedBatchEnqueueStatus.Closed:
                var closed = RadarProcessingQueuedBatchEnqueueResult.Closed(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(closed, pendingCount, pendingPayloadBytes);
                return closed;

            case RadarProcessingQueuedBatchEnqueueStatus.Faulted:
                var faulted = RadarProcessingQueuedBatchEnqueueResult.Faulted(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(faulted, pendingCount, pendingPayloadBytes);
                return faulted;

            default:
                RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(status);
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

}
