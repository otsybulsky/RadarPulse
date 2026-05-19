using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingQueuedBatch
{
    public RadarProcessingQueuedBatch(
        RadarProcessingQueuedBatchSequence sequence,
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        long enqueuedTimestamp = 0)
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

        Sequence = sequence;
        Batch = batch;
        OwnedSnapshotTime = ownedSnapshotTime;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
        EnqueuedTimestamp = enqueuedTimestamp;
        if (batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum))
        {
            PayloadValueCount = payloadValueCount;
            RawValueChecksum = rawValueChecksum;
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
