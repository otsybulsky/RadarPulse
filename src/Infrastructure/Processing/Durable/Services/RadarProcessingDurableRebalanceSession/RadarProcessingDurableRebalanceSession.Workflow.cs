using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableRebalanceSession
{
    /// <summary>
    /// Computes rebalance output for a claimed envelope and records the durable state.
    /// </summary>
    public async ValueTask<RadarProcessingDurableQueueOperationResult> ProcessClaimedAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(claimedEnvelope);

        var completion = await ComputeCompletionAsync(claimedEnvelope, cancellationToken)
            .ConfigureAwait(false);
        var operation = ApplyCompletionState(completion);
        if (operation.Status is RadarProcessingDurableQueueOperationStatus.Completed or
            RadarProcessingDurableQueueOperationStatus.Failed or
            RadarProcessingDurableQueueOperationStatus.Canceled)
        {
            AddCompletion(completion);
        }
        else
        {
            completion.Dispose();
        }

        return operation;
    }

    /// <summary>
    /// Commits ready completed rebalance envelopes in provider sequence order.
    /// </summary>
    /// <returns>Processing results published by this call.</returns>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CommitReady(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        List<RadarProcessingQueuedBatchProcessingResult>? published = null;
        while (true)
        {
            DurableRebalanceCompletion? completion;
            lock (sync)
            {
                if (faulted || canceled || !pendingCompletions.Remove(nextCommitSequence, out completion))
                {
                    break;
                }
            }

            try
            {
                var result = completion.Commit(
                    rebalanceSession,
                    asyncRebalanceSession,
                    cancellationToken);
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

    /// <summary>
    /// Claims, processes, and commits durable rebalance envelopes until no pending work remains.
    /// </summary>
    public async ValueTask<RadarProcessingDurableRebalanceSessionResult> DrainAsync(
        string workerId = "durable-local-worker",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(workerId);

        try
        {
            while (!IsFaulted && !IsCanceled)
            {
                var claim = queue.ClaimNext(workerId);
                if (claim.Status == RadarProcessingDurableQueueOperationStatus.Empty)
                {
                    break;
                }

                if (claim.Status != RadarProcessingDurableQueueOperationStatus.Claimed)
                {
                    MarkFaulted(claim.Message);
                    break;
                }

                await ProcessClaimedAsync(claim.ClaimedEnvelope!, cancellationToken)
                    .ConfigureAwait(false);
                CommitReady(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceled();
        }

        return CreateResult();
    }
}
