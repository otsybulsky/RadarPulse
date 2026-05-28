using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
    private async ValueTask ProcessDequeuedBatchAsync(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        try
        {
            using var consumerResourceLease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
            if (IsFaulted)
            {
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        queuedBatch.Sequence,
                        faultMessage));
                return;
            }

            var processingResult = asyncCoreSession is null
                ? core.Process(queuedBatch.Batch, cancellationToken)
                : await asyncCoreSession.ProcessAsync(queuedBatch.Batch, cancellationToken).ConfigureAwait(false);

            if (processingResult.IsValid)
            {
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Succeeded(
                        queuedBatch.Sequence,
                        processingResult));
                return;
            }

            var message = processingResult.Validation.Message;
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    message,
                    processingResult));
            MarkFaulted(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."));
            MarkCanceledAndRecordQueued();
        }
        catch (Exception exception)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message));
            MarkFaulted(exception.Message);
        }
    }
}
