using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
    private static RadarProcessingQueuedSessionResult CreateValidCompletedSession(ulong checksum) =>
        CreateSessionResult(
            [CreateAccepted(0)],
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    RadarProcessingQueuedBatchSequence.Initial,
                    CreateProcessingResult(checksum: checksum))
            ],
            completedCount: 1);

    private static RadarProcessingQueuedSessionResult CreateSessionResult(
        IReadOnlyCollection<RadarProcessingQueuedBatchEnqueueResult> enqueueResults,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingResults,
        long completedCount,
        RadarProcessingQueuedSessionStatus status = RadarProcessingQueuedSessionStatus.Completed,
        RadarProcessingTopologyVersion? finalTopologyVersion = null)
    {
        var accepted = enqueueResults.LongCount(static result => result.IsAccepted);
        var acceptedEventCount = 0L;
        var acceptedPayloadBytes = 0L;
        var acceptedPayloadValueCount = 0L;
        foreach (var enqueue in enqueueResults)
        {
            if (!enqueue.IsAccepted)
            {
                continue;
            }

            var batch = enqueue.Batch!;
            acceptedEventCount = checked(acceptedEventCount + batch.StreamEventCount);
            acceptedPayloadBytes = checked(acceptedPayloadBytes + batch.PayloadBytes);
            acceptedPayloadValueCount = checked(acceptedPayloadValueCount + batch.PayloadValueCount);
        }

        var failed = processingResults.LongCount(static result =>
            result.Status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceled = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingResults.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);
        var telemetry = new RadarProcessingProviderQueueTelemetrySummary(
            ownedSnapshotCount: accepted,
            ownedSnapshotPayloadBytes: acceptedPayloadBytes,
            enqueueAttemptCount: enqueueResults.Count,
            enqueuedBatchCount: accepted,
            ownedSnapshotEventCount: acceptedEventCount,
            ownedSnapshotPayloadValueCount: acceptedPayloadValueCount,
            dequeuedBatchCount: processingResults.Count,
            completedBatchCount: completedCount,
            failedBatchCount: failed,
            canceledBatchCount: canceled,
            skippedAfterFaultCount: skipped);

        return new RadarProcessingQueuedSessionResult(
            status,
            telemetry,
            enqueueResults,
            processingResults,
            finalTopologyVersion: finalTopologyVersion ?? RadarProcessingTopologyVersion.Initial);
    }

    private static RadarProcessingQueuedProviderValidationContext CreateValidationContext(
        RadarProcessingQueuedSessionResult session,
        long? releaseNotRequiredCount = null,
        TimeSpan? overlapElapsed = null)
    {
        var releaseCount = releaseNotRequiredCount ?? session.Telemetry.OwnedSnapshotCount;
        return new RadarProcessingQueuedProviderValidationContext(
            overlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            retentionStrategy: RadarProcessingRetainedPayloadStrategy.PooledCopy,
            retentionTelemetry: new RadarProcessingRetainedPayloadTelemetrySummary(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                retentionAttemptCount: session.Telemetry.EnqueueAttemptCount,
                retainedBatchCount: session.Telemetry.OwnedSnapshotCount,
                retainedEventCount: session.Telemetry.OwnedSnapshotEventCount,
                retainedPayloadBytes: session.Telemetry.OwnedSnapshotPayloadBytes,
                retainedPayloadValueCount: session.Telemetry.OwnedSnapshotPayloadValueCount,
                allocatedBytes: session.Telemetry.OwnedSnapshotAllocatedBytes,
                releaseAttemptCount: session.Telemetry.OwnedSnapshotCount,
                releaseNotRequiredCount: releaseCount),
            overlapElapsed: overlapElapsed ?? TimeSpan.FromMilliseconds(1));
    }

}
