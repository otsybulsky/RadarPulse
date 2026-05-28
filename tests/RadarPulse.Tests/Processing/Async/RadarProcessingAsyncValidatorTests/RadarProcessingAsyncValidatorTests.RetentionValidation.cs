using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    [Fact]
    public void WorkerTelemetryRetentionValidationObeysBounds()
    {
        var options = new RadarProcessingTelemetryRetentionOptions(
            RadarProcessingDiagnosticRetentionMode.Recent,
            maxRetainedWorkerBatches: 1,
            maxRetainedWorkerFailures: 1);
        var valid = new RadarProcessingWorkerTelemetrySummary(
            new RadarProcessingWorkerTelemetryCounters(
                dispatchedBatchCount: 2,
                failedBatchCount: 2,
                rejectedDispatchCount: 2),
            workerCount: 1,
            queueCapacity: 1,
            new[]
            {
                new RadarProcessingRecentWorkerBatch(
                    batchSequence: 2,
                    topologyVersion: RadarProcessingTopologyVersion.Initial,
                    workerCount: 1,
                    queueCapacity: 1,
                    submittedWorkItemCount: 1,
                    acceptedWorkItemCount: 0,
                    completedWorkItemCount: 0,
                    succeededWorkItemCount: 0,
                    failedWorkItemCount: 0,
                    canceledWorkItemCount: 0,
                    isSuccessful: false,
                    isRejected: true,
                    timedOut: false,
                    failureKind: RadarProcessingAsyncFailureKind.EnqueueRejected)
            },
            new[]
            {
                new RadarProcessingRecentWorkerFailure(
                    batchSequence: 2,
                    topologyVersion: RadarProcessingTopologyVersion.Initial,
                    failureKind: RadarProcessingAsyncFailureKind.EnqueueRejected)
            },
            new RadarProcessingWorkerRetentionStats(
                retainedBatchCount: 1,
                droppedBatchCount: 1,
                retainedFailureCount: 1,
                droppedFailureCount: 1));

        var validResult = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
            valid,
            options);

        Assert.True(validResult.IsValid);

        var invalid = new RadarProcessingWorkerTelemetrySummary(
            valid.Counters,
            workerCount: 1,
            queueCapacity: 1,
            new[]
            {
                valid.RecentBatches[0],
                new RadarProcessingRecentWorkerBatch(
                    batchSequence: 3,
                    topologyVersion: RadarProcessingTopologyVersion.Initial,
                    workerCount: 1,
                    queueCapacity: 1,
                    submittedWorkItemCount: 1,
                    acceptedWorkItemCount: 0,
                    completedWorkItemCount: 0,
                    succeededWorkItemCount: 0,
                    failedWorkItemCount: 0,
                    canceledWorkItemCount: 0,
                    isSuccessful: false,
                    isRejected: true,
                    timedOut: false,
                    failureKind: RadarProcessingAsyncFailureKind.EnqueueRejected)
            },
            valid.RecentFailures,
            new RadarProcessingWorkerRetentionStats(
                retainedBatchCount: 2,
                retainedFailureCount: 1));

        var invalidResult = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
            invalid,
            options);

        AssertInvalid(invalidResult, RadarProcessingAsyncValidationError.RetentionLimitExceeded);
    }
}
