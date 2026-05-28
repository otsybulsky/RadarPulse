using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    private static void AssertDirectBorrowedDefaultContour(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, result.ProviderMode);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(0, result.QueueCapacity);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.RetentionStrategy);
        Assert.Null(result.QueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, result.OverlapConsumerDelay);
        Assert.False(result.HasWorkerTelemetry);
        Assert.Null(result.WorkerTelemetry);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.False(result.HasQueueTelemetry);
        Assert.False(result.HasRetentionTelemetry);
        Assert.False(result.HasOverlapTelemetry);
        Assert.Equal(0, result.OwnedSnapshotAllocatedBytes);
        Assert.Equal(
            result.ProcessingCallbackAllocatedBytes,
            result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes);
        Assert.Equal(0, result.QueueTelemetry.EnqueueAttemptCount);
        Assert.Equal(0, result.RetentionTelemetry.RetentionAttemptCount);
        Assert.Equal(0, result.OverlapTelemetry.MeasuredAllocatedBytes);
        Assert.Equal(RadarProcessingRetainedResourcePressureSummary.Empty, result.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.HasRetainedPayloadPrewarm);
        Assert.Equal(0, result.RetainedPayloadPrewarmAllocatedBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertDirectBorrowedDefaultContour(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, result.ProviderMode);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(0, result.QueueCapacity);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.RetentionStrategy);
        Assert.Null(result.QueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, result.OverlapConsumerDelay);
        Assert.False(result.HasWorkerTelemetry);
        Assert.Null(result.WorkerTelemetry);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.False(result.HasQueueTelemetry);
        Assert.False(result.HasRetentionTelemetry);
        Assert.False(result.HasOverlapTelemetry);
        Assert.Equal(0, result.OwnedSnapshotAllocatedBytes);
        Assert.Equal(
            result.ProcessingCallbackAllocatedBytes,
            result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes);
        Assert.Equal(0, result.QueueTelemetry.EnqueueAttemptCount);
        Assert.Equal(0, result.RetentionTelemetry.RetentionAttemptCount);
        Assert.Equal(0, result.OverlapTelemetry.MeasuredAllocatedBytes);
        Assert.Equal(RadarProcessingRetainedResourcePressureSummary.Empty, result.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.HasRetainedPayloadPrewarm);
        Assert.Equal(0, result.RetainedPayloadPrewarmAllocatedBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertDefaultRetainedPayloadPrewarm(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            result.RetainedPayloadPrewarm.EventCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            result.RetainedPayloadPrewarm.PayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.RetainedPayloadPrewarmAllocatedBytes > 0);
        Assert.Equal(
            result.RetainedPayloadPrewarm.RetainedBytes,
            result.RetainedPayloadPrewarmRetainedBytes);
    }

    private static void AssertDefaultRetainedPayloadPrewarm(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            result.RetainedPayloadPrewarm.EventCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            result.RetainedPayloadPrewarm.PayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.RetainedPayloadPrewarmAllocatedBytes > 0);
        Assert.Equal(
            result.RetainedPayloadPrewarm.RetainedBytes,
            result.RetainedPayloadPrewarmRetainedBytes);
    }

    private static void AssertDirectQueuedOwnedRolloutContour(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode, result.ProviderMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode, result.ExecutionMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, result.QueueCapacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy, result.RetentionStrategy);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes, result.QueueRetainedPayloadBytes);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay, result.OverlapConsumerDelay);
        Assert.True(result.HasWorkerTelemetry);
        Assert.NotNull(result.WorkerTelemetry);
        var workerTelemetry = result.WorkerTelemetry!;
        Assert.True(
            RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                result.ProviderMode,
                result.ProviderOverlapMode,
                result.RetentionStrategy,
                result.ExecutionMode,
                new RadarProcessingAsyncExecutionOptions(
                    workerTelemetry.WorkerCount,
                    workerTelemetry.QueueCapacity),
                result.QueueCapacity,
                result.QueueRetainedPayloadBytes,
                result.OverlapConsumerDelay));
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, workerTelemetry.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            workerTelemetry.QueueCapacity);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.True(result.HasQueueTelemetry);
        Assert.True(result.HasRetentionTelemetry);
        Assert.True(result.HasOverlapTelemetry);
        Assert.Equal(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, result.OwnedSnapshotAllocatedBytes);
        Assert.True(result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes >= 0);
        Assert.True(result.QueueTelemetry.EnqueueAttemptCount > 0);
        Assert.True(result.RetentionTelemetry.RetentionAttemptCount > 0);
        Assert.Equal(0, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.OverlapTelemetry.ReleaseFailedCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionTelemetry.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        AssertRetainedPoolTelemetryReleased(result.RetentionTelemetry);
        Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertDirectQueuedOwnedRolloutContour(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode, result.ProviderMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode, result.ExecutionMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, result.QueueCapacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy, result.RetentionStrategy);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes, result.QueueRetainedPayloadBytes);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay, result.OverlapConsumerDelay);
        Assert.True(result.HasWorkerTelemetry);
        Assert.NotNull(result.WorkerTelemetry);
        var workerTelemetry = result.WorkerTelemetry!;
        Assert.True(
            RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                result.ProviderMode,
                result.ProviderOverlapMode,
                result.RetentionStrategy,
                result.ExecutionMode,
                new RadarProcessingAsyncExecutionOptions(
                    workerTelemetry.WorkerCount,
                    workerTelemetry.QueueCapacity),
                result.QueueCapacity,
                result.QueueRetainedPayloadBytes,
                result.OverlapConsumerDelay));
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, workerTelemetry.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            workerTelemetry.QueueCapacity);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.True(result.HasQueueTelemetry);
        Assert.True(result.HasRetentionTelemetry);
        Assert.True(result.HasOverlapTelemetry);
        Assert.Equal(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, result.OwnedSnapshotAllocatedBytes);
        Assert.True(result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes >= 0);
        Assert.True(result.QueueTelemetry.EnqueueAttemptCount > 0);
        Assert.True(result.RetentionTelemetry.RetentionAttemptCount > 0);
        Assert.Equal(0, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.OverlapTelemetry.ReleaseFailedCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionTelemetry.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        AssertRetainedPoolTelemetryReleased(result.RetentionTelemetry);
        Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertRetainedPoolTelemetryReleased(
        RadarProcessingRetainedPayloadTelemetrySummary telemetry)
    {
        Assert.Equal(
            telemetry.PoolRentCount,
            telemetry.EventPoolRentCount + telemetry.PayloadPoolRentCount);
        Assert.Equal(
            telemetry.PoolReturnCount,
            telemetry.EventPoolReturnCount + telemetry.PayloadPoolReturnCount);
        Assert.Equal(
            telemetry.PoolMissCount,
            telemetry.EventPoolMissCount + telemetry.PayloadPoolMissCount);
        Assert.True(telemetry.EventPoolRentCount > 0);
        Assert.True(telemetry.PayloadPoolRentCount > 0);
        Assert.Equal(telemetry.EventPoolRentCount, telemetry.EventPoolReturnCount);
        Assert.Equal(telemetry.PayloadPoolRentCount, telemetry.PayloadPoolReturnCount);
    }
}
