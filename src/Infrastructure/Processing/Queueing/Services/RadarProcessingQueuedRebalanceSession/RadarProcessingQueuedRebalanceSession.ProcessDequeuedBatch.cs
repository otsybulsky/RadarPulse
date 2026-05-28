using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
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

            var rebalanceResult = asyncRebalanceSession is null
                ? rebalanceSession.Process(queuedBatch.Batch, cancellationToken)
                : await asyncRebalanceSession.ProcessAsync(queuedBatch.Batch, cancellationToken).ConfigureAwait(false);

            var processingResult = rebalanceResult.ProcessingResult;
            if (!processingResult.IsValid)
            {
                var message = processingResult.Validation.Message;
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        message,
                        processingResult));
                MarkFaulted(message);
                return;
            }

            if (rebalanceResult.MigrationResult is { Succeeded: false } migrationResult)
            {
                var message = $"Queued rebalance migration failed with state {migrationResult.State}.";
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedMigration(
                        queuedBatch.Sequence,
                        message,
                        rebalanceResult));
                MarkFaulted(message);
                return;
            }

            if (!rebalanceResult.Validation.IsValid)
            {
                var message = rebalanceResult.Validation.Message;
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        message,
                        processingResult));
                MarkFaulted(message);
                return;
            }

            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    queuedBatch.Sequence,
                    rebalanceResult));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled."));
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
