using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingWorkerTelemetryContractTests
{
    [Fact]
    public void WorkerTelemetryCountersCarryAggregateCountsAndDurations()
    {
        var counters = new RadarProcessingWorkerTelemetryCounters(
            dispatchedBatchCount: 3,
            completedBatchCount: 1,
            failedBatchCount: 1,
            canceledBatchCount: 1,
            timedOutBatchCount: 1,
            rejectedDispatchCount: 1,
            submittedWorkItemCount: 12,
            acceptedWorkItemCount: 10,
            completedWorkItemCount: 9,
            succeededWorkItemCount: 6,
            failedWorkItemCount: 2,
            canceledWorkItemCount: 1,
            totalDispatchTime: TimeSpan.FromMilliseconds(11),
            totalQueueWaitTime: TimeSpan.FromMilliseconds(12),
            totalExecutionTime: TimeSpan.FromMilliseconds(13),
            totalAggregationTime: TimeSpan.FromMilliseconds(14),
            totalBarrierWaitTime: TimeSpan.FromMilliseconds(15));

        Assert.Equal(3, counters.DispatchedBatchCount);
        Assert.Equal(1, counters.CompletedBatchCount);
        Assert.Equal(1, counters.FailedBatchCount);
        Assert.Equal(1, counters.CanceledBatchCount);
        Assert.Equal(1, counters.TimedOutBatchCount);
        Assert.Equal(1, counters.RejectedDispatchCount);
        Assert.Equal(12, counters.SubmittedWorkItemCount);
        Assert.Equal(10, counters.AcceptedWorkItemCount);
        Assert.Equal(9, counters.CompletedWorkItemCount);
        Assert.Equal(6, counters.SucceededWorkItemCount);
        Assert.Equal(2, counters.FailedWorkItemCount);
        Assert.Equal(1, counters.CanceledWorkItemCount);
        Assert.Equal(TimeSpan.FromMilliseconds(11), counters.TotalDispatchTime);
        Assert.Equal(TimeSpan.FromMilliseconds(14), counters.TotalAggregationTime);
    }

    [Fact]
    public void WorkerTelemetryCountersRejectInvalidShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerTelemetryCounters(dispatchedBatchCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerTelemetryCounters(dispatchedBatchCount: 1, completedBatchCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerTelemetryCounters(completedWorkItemCount: 1, succeededWorkItemCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerTelemetryCounters(totalDispatchTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerTelemetryCounters(totalAggregationTime: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void RecentWorkerBatchCarriesStatusCountsAndTiming()
    {
        var batch = new RadarProcessingRecentWorkerBatch(
            batchSequence: 7,
            new RadarProcessingTopologyVersion(3),
            workerCount: 4,
            queueCapacity: 2,
            submittedWorkItemCount: 4,
            acceptedWorkItemCount: 4,
            completedWorkItemCount: 4,
            succeededWorkItemCount: 2,
            failedWorkItemCount: 1,
            canceledWorkItemCount: 1,
            isSuccessful: false,
            isRejected: true,
            timedOut: true,
            RadarProcessingAsyncFailureKind.TimedOut,
            RadarProcessingAsyncCancellationKind.Timeout,
            dispatchTime: TimeSpan.FromMilliseconds(1),
            queueWaitTime: TimeSpan.FromMilliseconds(2),
            executionTime: TimeSpan.FromMilliseconds(3),
            aggregationTime: TimeSpan.FromMilliseconds(4),
            barrierWaitTime: TimeSpan.FromMilliseconds(5));

        Assert.Equal(7, batch.BatchSequence);
        Assert.Equal(new RadarProcessingTopologyVersion(3), batch.TopologyVersion);
        Assert.Equal(4, batch.WorkerCount);
        Assert.Equal(2, batch.QueueCapacity);
        Assert.False(batch.IsSuccessful);
        Assert.True(batch.IsRejected);
        Assert.True(batch.TimedOut);
        Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, batch.FailureKind);
        Assert.Equal(RadarProcessingAsyncCancellationKind.Timeout, batch.CancellationKind);
        Assert.Equal(TimeSpan.FromMilliseconds(4), batch.AggregationTime);
    }

    [Fact]
    public void RecentWorkerBatchRejectsInvalidShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(batchSequence: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(workerCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(queueCapacity: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(completedWorkItemCount: 1, succeededWorkItemCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(failureKind: (RadarProcessingAsyncFailureKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(cancellationKind: (RadarProcessingAsyncCancellationKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateBatch(dispatchTime: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void RecentWorkerFailureCarriesCompactCodesOnly()
    {
        var failure = new RadarProcessingRecentWorkerFailure(
            batchSequence: 5,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingAsyncFailureKind.WorkerException,
            workItemId: 2,
            workerId: new RadarProcessingWorkerId(3),
            shardId: 4);

        Assert.Equal(5, failure.BatchSequence);
        Assert.Equal(RadarProcessingAsyncFailureKind.WorkerException, failure.FailureKind);
        Assert.Equal(RadarProcessingAsyncCancellationKind.None, failure.CancellationKind);
        Assert.Equal(2, failure.WorkItemId);
        Assert.Equal(new RadarProcessingWorkerId(3), failure.WorkerId);
        Assert.Equal(4, failure.ShardId);
    }

    [Fact]
    public void RecentWorkerFailureRejectsInvalidShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRecentWorkerFailure(-1, RadarProcessingTopologyVersion.Initial, RadarProcessingAsyncFailureKind.WorkerException));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRecentWorkerFailure(1, RadarProcessingTopologyVersion.Initial, (RadarProcessingAsyncFailureKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRecentWorkerFailure(
                1,
                RadarProcessingTopologyVersion.Initial,
                cancellationKind: (RadarProcessingAsyncCancellationKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRecentWorkerFailure(
                1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingAsyncFailureKind.WorkerException,
                workItemId: -1));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingRecentWorkerFailure(1, RadarProcessingTopologyVersion.Initial));
    }

    [Fact]
    public void WorkerTelemetrySummaryCopiesRecentCollections()
    {
        var batches = new List<RadarProcessingRecentWorkerBatch>
        {
            CreateBatch(batchSequence: 1)
        };
        var failures = new List<RadarProcessingRecentWorkerFailure>
        {
            new(
                1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingAsyncFailureKind.WorkerException)
        };
        var summary = new RadarProcessingWorkerTelemetrySummary(
            new RadarProcessingWorkerTelemetryCounters(dispatchedBatchCount: 1),
            workerCount: 2,
            queueCapacity: 3,
            batches,
            failures,
            new RadarProcessingWorkerRetentionStats(retainedBatchCount: 1, retainedFailureCount: 1));

        batches.Clear();
        failures.Clear();

        Assert.Equal(2, summary.WorkerCount);
        Assert.Equal(3, summary.QueueCapacity);
        Assert.Single(summary.RecentBatches);
        Assert.Single(summary.RecentFailures);
        Assert.Equal(1, summary.RetentionStats.RetainedBatchCount);
        Assert.Empty(RadarProcessingWorkerTelemetrySummary.Empty.RecentBatches);
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingWorkerTelemetrySummary(null!, 0, 0, Array.Empty<RadarProcessingRecentWorkerBatch>(), Array.Empty<RadarProcessingRecentWorkerFailure>(), new RadarProcessingWorkerRetentionStats()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerTelemetrySummary(new RadarProcessingWorkerTelemetryCounters(), -1, 0, Array.Empty<RadarProcessingRecentWorkerBatch>(), Array.Empty<RadarProcessingRecentWorkerFailure>(), new RadarProcessingWorkerRetentionStats()));
    }

    [Fact]
    public void WorkerRetentionStatsReportDroppedDetail()
    {
        var stats = new RadarProcessingWorkerRetentionStats(
            retainedBatchCount: 1,
            droppedBatchCount: 2,
            retainedFailureCount: 3,
            droppedFailureCount: 4);

        Assert.Equal(1, stats.RetainedBatchCount);
        Assert.Equal(2, stats.DroppedBatchCount);
        Assert.Equal(3, stats.RetainedFailureCount);
        Assert.Equal(4, stats.DroppedFailureCount);
        Assert.True(stats.HasDroppedDetail);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerRetentionStats(retainedBatchCount: -1));
    }

    private static RadarProcessingRecentWorkerBatch CreateBatch(
        long batchSequence = 1,
        int workerCount = 1,
        int queueCapacity = 1,
        int completedWorkItemCount = 1,
        int succeededWorkItemCount = 1,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None,
        TimeSpan dispatchTime = default) =>
        new(
            batchSequence,
            RadarProcessingTopologyVersion.Initial,
            workerCount,
            queueCapacity,
            submittedWorkItemCount: 1,
            acceptedWorkItemCount: 1,
            completedWorkItemCount,
            succeededWorkItemCount,
            failedWorkItemCount: 0,
            canceledWorkItemCount: 0,
            isSuccessful: failureKind == RadarProcessingAsyncFailureKind.None,
            isRejected: false,
            timedOut: false,
            failureKind,
            cancellationKind,
            dispatchTime);
}
