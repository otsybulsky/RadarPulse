using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Owned batch retained by a queued provider until processing consumes it.
/// </summary>
/// <remarks>
/// Queued batches must own their payload. This prevents borrowed archive buffers
/// from escaping the producer boundary while processing overlaps with intake.
/// Payload metrics are captured at enqueue time so validation and telemetry can
/// reason about deterministic counts even after the batch has moved through the
/// queue.
/// </remarks>
public sealed class RadarProcessingQueuedBatch
{
    /// <summary>
    /// Creates a queued batch with owned payload and optional precomputed payload metrics.
    /// </summary>
    public RadarProcessingQueuedBatch(
        RadarProcessingQueuedBatchSequence sequence,
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        long enqueuedTimestamp = 0,
        long? payloadValueCount = null,
        long? rawValueChecksum = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Lifetime != RadarEventBatchLifetime.Owned)
        {
            throw new ArgumentException("Queued provider batches must own their payload.", nameof(batch));
        }

        if (ownedSnapshotTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ownedSnapshotTime));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueuedTimestamp);
        if (payloadValueCount.HasValue != rawValueChecksum.HasValue)
        {
            throw new ArgumentException(
                "Payload value count and raw value checksum must be supplied together.",
                nameof(payloadValueCount));
        }

        if (payloadValueCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount.Value);
            ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum!.Value);
        }

        Sequence = sequence;
        Batch = batch;
        OwnedSnapshotTime = ownedSnapshotTime;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
        EnqueuedTimestamp = enqueuedTimestamp;
        if (payloadValueCount.HasValue)
        {
            PayloadValueCount = payloadValueCount.Value;
            RawValueChecksum = rawValueChecksum!.Value;
        }
        else if (batch.TryGetPayloadMetrics(out var computedPayloadValueCount, out var computedRawValueChecksum))
        {
            PayloadValueCount = computedPayloadValueCount;
            RawValueChecksum = computedRawValueChecksum;
        }
    }

    /// <summary>
    /// Provider ordering key assigned at enqueue time.
    /// </summary>
    public RadarProcessingQueuedBatchSequence Sequence { get; }

    /// <summary>
    /// Owned radar event batch payload.
    /// </summary>
    public RadarEventBatch Batch { get; }

    /// <summary>
    /// Time spent creating the owned snapshot before enqueue.
    /// </summary>
    public TimeSpan OwnedSnapshotTime { get; }

    /// <summary>
    /// Allocated bytes attributed to the owned snapshot operation.
    /// </summary>
    public long OwnedSnapshotAllocatedBytes { get; }

    /// <summary>
    /// Monotonic timestamp captured when the batch entered the provider queue.
    /// </summary>
    public long EnqueuedTimestamp { get; }

    /// <summary>
    /// Number of stream events in the queued batch.
    /// </summary>
    public int StreamEventCount => Batch.EventCount;

    /// <summary>
    /// Payload byte length retained by the queued batch.
    /// </summary>
    public int PayloadBytes => Batch.PayloadLength;

    /// <summary>
    /// Count of payload values used by validation and telemetry.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Deterministic checksum of raw payload values.
    /// </summary>
    public long RawValueChecksum { get; }
}
