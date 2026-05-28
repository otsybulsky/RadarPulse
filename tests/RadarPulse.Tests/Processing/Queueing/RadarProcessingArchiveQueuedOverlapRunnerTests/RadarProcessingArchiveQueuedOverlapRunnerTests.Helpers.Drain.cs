using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunnerTests
{
    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainAllAsync(
        RadarProcessingOwnedBatchQueue queue,
        CancellationToken cancellationToken)
    {
        var processingResults = new List<RadarProcessingQueuedBatchProcessingResult>();
        while (true)
        {
            var dequeue = await queue.DequeueAsync(cancellationToken);
            switch (dequeue.Status)
            {
                case RadarProcessingOwnedBatchDequeueStatus.Item:
                    processingResults.Add(
                        RadarProcessingQueuedBatchProcessingResult.Succeeded(
                            dequeue.Batch!.Sequence,
                            CreateProcessingResult()));
                    break;

                case RadarProcessingOwnedBatchDequeueStatus.Closed:
                    return CreateSessionResult(queue, processingResults);

                default:
                    return CreateSessionResult(
                        queue,
                        processingResults,
                        RadarProcessingQueuedSessionStatus.Faulted,
                        dequeue.Message);
            }
        }
    }

    private static RadarProcessingQueuedSessionResult CreateSessionResult(
        RadarProcessingOwnedBatchQueue queue,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingResults,
        RadarProcessingQueuedSessionStatus status = RadarProcessingQueuedSessionStatus.Completed,
        string message = "")
    {
        var queueSummary = queue.CreateTelemetrySummary();
        var completed = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        var failed = processingResults.LongCount(static result =>
            result.Status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceled = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);
        var telemetry = new RadarProcessingProviderQueueTelemetrySummary(
            queueSummary.OwnedSnapshotCount,
            queueSummary.OwnedSnapshotPayloadBytes,
            queueSummary.OwnedSnapshotAllocatedBytes,
            queueSummary.TotalOwnedSnapshotTime,
            queueSummary.EnqueueAttemptCount,
            queueSummary.EnqueuedBatchCount,
            queueSummary.EnqueueFullCount,
            queueSummary.EnqueueTimedOutCount,
            queueSummary.EnqueueCanceledCount,
            queueSummary.EnqueueClosedCount,
            queueSummary.EnqueueFaultedCount,
            queueSummary.TotalEnqueueWaitTime,
            queueSummary.DequeuedBatchCount,
            completed,
            failed,
            canceled,
            skipped,
            queueSummary.TotalDrainTime,
            queueSummary.QueueDepthHighWatermark,
            queueSummary.QueuedPayloadBytesHighWatermark,
            queueSummary.OwnedSnapshotPayloadValueCount,
            queueSummary.TotalProviderToProcessingLatency,
            queueSummary.RecentDetails,
            queueSummary.DroppedRecentDetailCount,
            queueSummary.OwnedSnapshotEventCount,
            queueSummary.TotalDequeueWaitTime);

        return new RadarProcessingQueuedSessionResult(
            status,
            telemetry,
            processingResults: processingResults,
            message: message);
    }

}
