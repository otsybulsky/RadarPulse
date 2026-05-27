using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingQueuedBatch
{
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

    public RadarProcessingQueuedBatchSequence Sequence { get; }

    public RadarEventBatch Batch { get; }

    public TimeSpan OwnedSnapshotTime { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public long EnqueuedTimestamp { get; }

    public int StreamEventCount => Batch.EventCount;

    public int PayloadBytes => Batch.PayloadLength;

    public long PayloadValueCount { get; }

    public long RawValueChecksum { get; }
}
