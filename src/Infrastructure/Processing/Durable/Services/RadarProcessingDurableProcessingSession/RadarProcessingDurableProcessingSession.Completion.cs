using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    private async ValueTask<DurableProcessingCompletion> ComputeCompletionAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ComputeCompletionAsync(
                    claimedEnvelope.BatchId,
                    claimedEnvelope.QueuedBatch,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    claimedEnvelope.ProviderSequence,
                    "Durable processing batch was canceled."));
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return DurableProcessingCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    claimedEnvelope.ProviderSequence,
                    exception.Message,
                    result));
        }
        catch (Exception exception)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    claimedEnvelope.ProviderSequence,
                    exception.Message));
        }
    }

    private async ValueTask<DurableProcessingCompletion> ComputeCompletionAsync(
        RadarProcessingDurableBatchId batchId,
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return DurableProcessingCompletion.FromProcessingResult(
                    batchId,
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Durable processing batch was canceled."));
            }

            var invalid = core.ValidateBatchForProcessing(
                queuedBatch.Batch,
                cancellationToken);
            if (invalid is not null)
            {
                return DurableProcessingCompletion.FromProcessingResult(
                    batchId,
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid));
            }

            if (asyncCoreSession is not null)
            {
                var asyncDelta = await asyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .ConfigureAwait(false);
                return DurableProcessingCompletion.FromAsyncDelta(
                    batchId,
                    queuedBatch.Sequence,
                    asyncDelta);
            }

            var delta = core.ComputeProcessingDelta(
                queuedBatch.Batch,
                cancellationToken);
            return DurableProcessingCompletion.FromDelta(
                batchId,
                queuedBatch.Sequence,
                delta);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                batchId,
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Durable processing batch was canceled."));
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return DurableProcessingCompletion.FromProcessingResult(
                batchId,
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result));
        }
        catch (Exception exception)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                batchId,
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message));
        }
    }

    private RadarProcessingDurableQueueOperationResult ApplyCompletionState(
        DurableProcessingCompletion completion)
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
        DurableProcessingCompletion completion)
    {
        lock (sync)
        {
            if (!pendingCompletions.TryAdd(completion.Sequence.Value, completion))
            {
                throw new InvalidOperationException(
                    $"Durable processing sequence {completion.Sequence.Value} has already completed.");
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

    private static RadarProcessingAsyncCoreSession? CreateAsyncCoreSessionIfNeeded(
        RadarProcessingCore core)
    {
        ArgumentNullException.ThrowIfNull(core);
        return core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncCoreSession(core)
            : null;
    }
}
