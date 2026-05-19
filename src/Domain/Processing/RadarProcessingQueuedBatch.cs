using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingQueuedBatch
{
    public RadarProcessingQueuedBatch(
        RadarProcessingQueuedBatchSequence sequence,
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0)
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

        Sequence = sequence;
        Batch = batch;
        OwnedSnapshotTime = ownedSnapshotTime;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
    }

    public RadarProcessingQueuedBatchSequence Sequence { get; }

    public RadarEventBatch Batch { get; }

    public TimeSpan OwnedSnapshotTime { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public int StreamEventCount => Batch.EventCount;

    public int PayloadBytes => Batch.PayloadLength;
}
