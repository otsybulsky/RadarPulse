using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Commits ready completed envelopes in provider sequence order.
    /// </summary>
    /// <returns>Processing results published by this call.</returns>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CommitReady(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        List<RadarProcessingQueuedBatchProcessingResult>? published = null;
        while (true)
        {
            DurableProcessingCompletion? completion;
            lock (sync)
            {
                if (faulted || canceled || !pendingCompletions.Remove(nextCommitSequence, out completion))
                {
                    break;
                }
            }

            try
            {
                var result = completion.Commit(core, cancellationToken);
                if (result.IsSuccessful)
                {
                    queue.MarkCommitted(completion.BatchId);
                    queue.MarkReleased(completion.BatchId);
                }
                else if (result.Status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                         RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                         RadarProcessingQueuedBatchProcessingStatus.FailedMigration)
                {
                    queue.Fail(completion.BatchId, result.Message);
                    MarkFaulted(result.Message);
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    queue.Cancel(completion.BatchId, result.Message);
                    MarkCanceled();
                }

                RecordProcessingResult(result);
                published ??= [];
                published.Add(result);
                nextCommitSequence = checked(nextCommitSequence + 1);
            }
            finally
            {
                completion.Dispose();
            }
        }

        return published is null
            ? Array.Empty<RadarProcessingQueuedBatchProcessingResult>()
            : Array.AsReadOnly(published.ToArray());
    }
}
