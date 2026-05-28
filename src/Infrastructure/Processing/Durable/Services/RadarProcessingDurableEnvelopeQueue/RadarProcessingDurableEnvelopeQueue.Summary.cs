using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableEnvelopeQueue
{
    public RadarProcessingDurableQueueSummary CreateSummary()
    {
        lock (sync)
        {
            long pending = 0;
            long claimed = 0;
            long completed = 0;
            long committed = 0;
            long failed = 0;
            long poison = 0;
            long abandoned = 0;
            long canceled = 0;
            long released = 0;
            long retry = 0;
            RadarProcessingQueuedBatchSequence? oldestUncommitted = null;
            RadarProcessingDurableBatchId? firstBlockingBatchId = null;
            RadarProcessingQueuedBatchSequence? firstBlockingSequence = null;
            RadarProcessingDurableEnvelopeState? firstBlockingState = null;
            string firstBlockingReason = string.Empty;

            foreach (var entry in bySequence.Values)
            {
                switch (entry.State)
                {
                    case RadarProcessingDurableEnvelopeState.Pending:
                        pending++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Claimed:
                        claimed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Completed:
                        completed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Committed:
                        committed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Failed:
                        failed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Poison:
                        poison++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Abandoned:
                        abandoned++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Canceled:
                        canceled++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Released:
                        released++;
                        break;
                    default:
                        RadarProcessingDurableEnvelopeSnapshot.EnsureKnownState(entry.State);
                        throw new ArgumentOutOfRangeException(nameof(entry));
                }

                retry += Math.Max(0, entry.Attempt - 1);
                if (entry.State is RadarProcessingDurableEnvelopeState.Committed or
                    RadarProcessingDurableEnvelopeState.Released)
                {
                    continue;
                }

                oldestUncommitted ??= entry.QueuedBatch.Sequence;
                if (!firstBlockingBatchId.HasValue)
                {
                    firstBlockingBatchId = entry.BatchId;
                    firstBlockingSequence = entry.QueuedBatch.Sequence;
                    firstBlockingState = entry.State;
                    firstBlockingReason = CreateBlockingReason(entry);
                }
            }

            return new RadarProcessingDurableQueueSummary(
                byBatchId.Count,
                pending,
                claimed,
                completed,
                committed,
                failed,
                poison,
                abandoned,
                canceled,
                released,
                retry,
                oldestUncommitted,
                firstBlockingBatchId,
                firstBlockingSequence,
                firstBlockingState,
                firstBlockingReason);
        }
    }

    /// <summary>
    /// Cancels all pending, claimed, and completed envelopes.
    /// </summary>
}
