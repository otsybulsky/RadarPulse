using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
{
    private OrderedConcurrentRebalanceBatchWork StartOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentRebalanceBatchWork(task);
    }

    private OrderedConcurrentRebalanceBatchCompletion CreateSkippedAfterFaultCompletion(
        RadarProcessingQueuedBatch queuedBatch)
    {
        using var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                queuedBatch.Sequence,
                faultMessage));
    }

    private OrderedConcurrentRebalanceBatchCompletion ComputeOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued rebalance batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = rebalanceSession.Core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            if (asyncRebalanceSession is not null)
            {
                var asyncDelta = asyncRebalanceSession
                    .AsyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                return OrderedConcurrentRebalanceBatchCompletion.FromAsyncDelta(
                    queuedBatch.Sequence,
                    queuedBatch.Batch,
                    asyncDelta,
                    lease);
            }

            var delta = rebalanceSession.Core.ComputeProcessingDelta(queuedBatch.Batch, cancellationToken);
            return OrderedConcurrentRebalanceBatchCompletion.FromDelta(
                queuedBatch.Sequence,
                queuedBatch.Batch,
                delta,
                lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = rebalanceSession.Core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private void PublishReadyOrderedCompletions(
        List<OrderedConcurrentRebalanceBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation)
    {
        if (nextPublishSequence < 0)
        {
            return;
        }

        while (true)
        {
            var index = FindCompletionIndex(completed, nextPublishSequence);
            if (index < 0)
            {
                return;
            }

            var completion = completed[index];
            completed.RemoveAt(index);
            try
            {
                var result = IsFaulted
                    ? RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        completion.Sequence,
                        faultMessage)
                    : completion.Commit(
                        rebalanceSession,
                        asyncRebalanceSession,
                        activeCancellation.Token);
                RecordProcessingResult(result);
                nextPublishSequence++;

                if (IsFailedProcessingStatus(result.Status))
                {
                    MarkFaulted(result.Message);
                    activeCancellation.Cancel();
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    activeCancellation.Cancel();
                    MarkCanceledAndRecordQueued();
                }
            }
            finally
            {
                completion.Dispose();
            }
        }
    }

    private static int FindActiveWorkIndex(
        List<OrderedConcurrentRebalanceBatchWork> active,
        Task<OrderedConcurrentRebalanceBatchCompletion> completedTask)
    {
        for (var i = 0; i < active.Count; i++)
        {
            if (ReferenceEquals(active[i].Task, completedTask))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Completed ordered rebalance task was not found.");
    }

    private static int FindCompletionIndex(
        List<OrderedConcurrentRebalanceBatchCompletion> completed,
        long sequence)
    {
        for (var i = 0; i < completed.Count; i++)
        {
            if (completed[i].Sequence.Value == sequence)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsFailedProcessingStatus(
        RadarProcessingQueuedBatchProcessingStatus status) =>
        status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration;

    private static async ValueTask CompleteAndDiscardActiveWorkAsync(
        List<OrderedConcurrentRebalanceBatchWork> active)
    {
        foreach (var work in active)
        {
            var completion = await work.Task.ConfigureAwait(false);
            completion.Dispose();
        }

        active.Clear();
    }

}
