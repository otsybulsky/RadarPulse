using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableEnvelopeQueue
{
    public RadarProcessingDurableQueueOperationResult Accept(
        RadarProcessingDurableBatchId batchId,
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0)
    {
        EnsureValidBatchId(batchId);
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Lifetime != RadarEventBatchLifetime.Owned)
        {
            throw new ArgumentException("Durable envelope queue accepts only owned RadarEventBatch values.", nameof(batch));
        }

        if (ownedSnapshotTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ownedSnapshotTime));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);

        lock (sync)
        {
            if (byBatchId.TryGetValue(batchId, out var existing))
            {
                return RadarProcessingDurableQueueOperationResult.Duplicate(existing.ToSnapshot());
            }

            var sequence = new RadarProcessingQueuedBatchSequence(nextSequence);
            var queuedBatch = new RadarProcessingQueuedBatch(
                sequence,
                batch,
                ownedSnapshotTime,
                ownedSnapshotAllocatedBytes,
                Stopwatch.GetTimestamp());
            var entry = new EnvelopeEntry(
                batchId,
                queuedBatch,
                Stopwatch.GetTimestamp());

            byBatchId.Add(batchId, entry);
            bySequence.Add(sequence.Value, entry);
            nextSequence = checked(nextSequence + 1);
            PersistLocked();

            return RadarProcessingDurableQueueOperationResult.Accepted(entry.ToSnapshot());
        }
    }

    /// <summary>
    /// Claims the lowest-sequence pending envelope for a worker.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult ClaimNext(
        string workerId = "")
    {
        ArgumentNullException.ThrowIfNull(workerId);

        lock (sync)
        {
            foreach (var entry in bySequence.Values)
            {
                if (entry.State != RadarProcessingDurableEnvelopeState.Pending)
                {
                    continue;
                }

                if (entry.Attempt == 0)
                {
                    entry.Attempt = 1;
                }

                entry.State = RadarProcessingDurableEnvelopeState.Claimed;
                entry.WorkerId = workerId;
                entry.Message = string.Empty;
                entry.ClaimedTimestamp = Stopwatch.GetTimestamp();
                PersistLocked();

                var snapshot = entry.ToSnapshot();
                return RadarProcessingDurableQueueOperationResult.Claimed(
                    new RadarProcessingDurableClaimedEnvelope(snapshot, entry.QueuedBatch));
            }

            return RadarProcessingDurableQueueOperationResult.Empty();
        }
    }

    /// <summary>
    /// Marks a claimed envelope as completed before ordered commit.
}
