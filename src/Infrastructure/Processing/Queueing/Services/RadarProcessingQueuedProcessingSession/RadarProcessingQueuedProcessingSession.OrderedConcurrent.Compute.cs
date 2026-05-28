using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private OrderedConcurrentBatchWork StartOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentBatchWork(task);
    }

    private OrderedConcurrentBatchWork StartOrderedConcurrentHandlerDeltaBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentHandlerDeltaBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentBatchWork(task);
    }

    private OrderedConcurrentBatchCompletion CreateSkippedAfterFaultCompletion(
        RadarProcessingQueuedBatch queuedBatch)
    {
        using var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        return OrderedConcurrentBatchCompletion.FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                queuedBatch.Sequence,
                faultMessage));
    }

    private OrderedConcurrentBatchCompletion ComputeOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued processing batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            if (asyncCoreSession is not null)
            {
                var asyncDelta = asyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                return OrderedConcurrentBatchCompletion.FromAsyncDelta(queuedBatch.Sequence, asyncDelta, lease);
            }

            var delta = core.ComputeProcessingDelta(queuedBatch.Batch, cancellationToken);
            return OrderedConcurrentBatchCompletion.FromDelta(queuedBatch.Sequence, delta, lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private OrderedConcurrentBatchCompletion ComputeOrderedConcurrentHandlerDeltaBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued processing batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            var delta = core.ComputeProcessingDeltaForHandlerDeltaMerge(queuedBatch.Batch, cancellationToken);
            var handlerDeltas = CreateHandlerDeltas(queuedBatch, delta, cancellationToken);
            return OrderedConcurrentBatchCompletion.FromHandlerDeltas(
                queuedBatch.Sequence,
                delta,
                handlerDeltas,
                lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

}
