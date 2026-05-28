using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableRebalanceSession
{
    private async ValueTask<DurableRebalanceCompletion> ComputeCompletionAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return DurableRebalanceCompletion.FromProcessingResult(
                    claimedEnvelope.BatchId,
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        claimedEnvelope.ProviderSequence,
                        "Durable rebalance batch was canceled."));
            }

            var invalid = rebalanceSession.Core.ValidateBatchForProcessing(
                claimedEnvelope.QueuedBatch.Batch,
                cancellationToken);
            if (invalid is not null)
            {
                return DurableRebalanceCompletion.FromProcessingResult(
                    claimedEnvelope.BatchId,
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        claimedEnvelope.ProviderSequence,
                        invalid.Validation.Message,
                        invalid));
            }

            if (asyncRebalanceSession is not null)
            {
                var asyncDelta = await asyncRebalanceSession
                    .AsyncCoreSession
                    .ComputeDeltaAsync(claimedEnvelope.QueuedBatch.Batch, cancellationToken)
                    .ConfigureAwait(false);
                return DurableRebalanceCompletion.FromAsyncDelta(
                    claimedEnvelope.BatchId,
                    claimedEnvelope.ProviderSequence,
                    claimedEnvelope.QueuedBatch.Batch,
                    asyncDelta);
            }

            var delta = rebalanceSession.Core.ComputeProcessingDelta(
                claimedEnvelope.QueuedBatch.Batch,
                cancellationToken);
            return DurableRebalanceCompletion.FromDelta(
                claimedEnvelope.BatchId,
                claimedEnvelope.ProviderSequence,
                claimedEnvelope.QueuedBatch.Batch,
                delta);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DurableRebalanceCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    claimedEnvelope.ProviderSequence,
                    "Durable rebalance batch was canceled."));
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = rebalanceSession.Core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return DurableRebalanceCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    claimedEnvelope.ProviderSequence,
                    exception.Message,
                    result));
        }
        catch (Exception exception)
        {
            return DurableRebalanceCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    claimedEnvelope.ProviderSequence,
                    exception.Message));
        }
    }

    private RadarProcessingDurableQueueOperationResult ApplyCompletionState(
        DurableRebalanceCompletion completion)
    {
        var result = completion.ProcessingResult;
        if (result is null)
        {
            return queue.Complete(completion.BatchId);
        }

        return result.Status switch
        {
            RadarProcessingQueuedBatchProcessingStatus.Canceled =>
                queue.Cancel(completion.BatchId, result.Message),
            RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                RadarProcessingQueuedBatchProcessingStatus.FailedMigration =>
                queue.Fail(completion.BatchId, result.Message),
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault =>
                queue.Cancel(completion.BatchId, result.Message),
            _ => queue.Complete(completion.BatchId)
        };
    }

    private void AddCompletion(
        DurableRebalanceCompletion completion)
    {
        lock (sync)
        {
            if (!pendingCompletions.TryAdd(completion.Sequence.Value, completion))
            {
                throw new InvalidOperationException(
                    $"Durable rebalance sequence {completion.Sequence.Value} has already completed.");
            }
        }
    }

    private void DisposePendingCompletions()
    {
        lock (sync)
        {
            foreach (var completion in pendingCompletions.Values)
            {
                completion.Dispose();
            }

            pendingCompletions.Clear();
        }
    }

    private void RecordProcessingResult(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        lock (sync)
        {
            processingResults.Add(result);
        }
    }

    private void MarkFaulted(
        string message)
    {
        lock (sync)
        {
            faulted = true;
            faultMessage = message;
        }
    }

    private void MarkCanceled()
    {
        lock (sync)
        {
            canceled = true;
        }
    }

    private static RadarProcessingAsyncRebalanceSession? CreateAsyncRebalanceSessionIfNeeded(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncRebalanceSession(rebalanceSession)
            : null;
    }
}
