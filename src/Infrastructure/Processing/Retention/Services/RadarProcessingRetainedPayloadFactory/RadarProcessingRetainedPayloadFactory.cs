using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Creates owned retained payload batches using snapshot or pooled-copy strategies.
/// </summary>
/// <remarks>
/// Pooled-copy retention transfers release responsibility through
/// <see cref="RadarProcessingRetainedBatchResource"/>. If copying fails, rented
/// arrays are returned before a failed retention result is produced.
/// </remarks>
public sealed partial class RadarProcessingRetainedPayloadFactory
{
    private readonly ArrayPool<RadarStreamEvent> eventPool;
    private readonly ArrayPool<byte> payloadPool;

    /// <summary>
    /// Creates a factory with retained event and payload array pools.
    /// </summary>
    public RadarProcessingRetainedPayloadFactory()
        : this(new RadarProcessingRetainedEventArrayPool(), new RadarProcessingRetainedPayloadByteArrayPool())
    {
    }

    /// <summary>
    /// Creates a factory over explicit event and payload pools.
    /// </summary>
    public RadarProcessingRetainedPayloadFactory(
        ArrayPool<RadarStreamEvent> eventPool,
        ArrayPool<byte> payloadPool)
    {
        this.eventPool = eventPool ?? throw new ArgumentNullException(nameof(eventPool));
        this.payloadPool = payloadPool ?? throw new ArgumentNullException(nameof(payloadPool));
    }

    /// <summary>
    /// Retains a batch according to the selected retained payload strategy.
    /// </summary>
    /// <returns>
    /// A succeeded result with an owned retained batch and optional release
    /// resource, or a canceled/unsupported/failed result with diagnostics.
    /// </returns>
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
}
