using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerTelemetryRecorderTests
{
    [Fact]
    public void RecorderRetainsBoundedRecentBatchesAndFailures()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedWorkerBatches: 2,
                maxRetainedWorkerFailures: 1));

        recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 1))));
        recorder.RecordDispatch(CreateDispatch(CreateFailedResult(CreatePlan(batchSequence: 2))));
        recorder.RecordDispatch(CreateDispatch(CreateTimedOutResult(CreatePlan(batchSequence: 3))));
        var summary = recorder.CreateSummary();

        Assert.Equal(3, summary.Counters.DispatchedBatchCount);
        Assert.Equal(1, summary.Counters.CompletedBatchCount);
        Assert.Equal(2, summary.Counters.FailedBatchCount);
        Assert.Equal(1, summary.Counters.TimedOutBatchCount);
        Assert.Equal(1, summary.Counters.RejectedDispatchCount);
        Assert.Equal(new long[] { 2, 3 }, summary.RecentBatches.Select(static batch => batch.BatchSequence));
        Assert.Single(summary.RecentFailures);
        Assert.Equal(3, summary.RecentFailures[0].BatchSequence);
        Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, summary.RecentFailures[0].FailureKind);
        Assert.True(summary.RecentFailures[0].TimedOut);
        Assert.Equal(1, summary.RetentionStats.DroppedBatchCount);
        Assert.Equal(1, summary.RetentionStats.RetainedFailureCount);
        Assert.True(summary.RetentionStats.DroppedFailureCount > 0);
        Assert.True(summary.RetentionStats.HasDroppedDetail);
    }

    [Fact]
    public void RecorderCountersOnlyRetentionDropsAllRecentDetail()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Counters,
                maxRetainedWorkerBatches: 10,
                maxRetainedWorkerFailures: 10));

        recorder.RecordDispatch(CreateDispatch(CreateFailedResult(CreatePlan(batchSequence: 1))));
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.DispatchedBatchCount);
        Assert.Equal(1, summary.Counters.FailedBatchCount);
        Assert.Equal(1, summary.Counters.FailedWorkItemCount);
        Assert.Empty(summary.RecentBatches);
        Assert.Empty(summary.RecentFailures);
        Assert.Equal(1, summary.RetentionStats.DroppedBatchCount);
        Assert.Equal(1, summary.RetentionStats.DroppedFailureCount);
    }

    [Fact]
    public void RecorderRecordsCancellationCodesWithoutFailureText()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder();
        var plan = CreatePlan(batchSequence: 5);

        recorder.RecordDispatch(CreateDispatch(CreateCanceledResult(plan)));
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.DispatchedBatchCount);
        Assert.Equal(0, summary.Counters.FailedBatchCount);
        Assert.Equal(1, summary.Counters.CanceledBatchCount);
        Assert.Equal(2, summary.Counters.CanceledWorkItemCount);
        Assert.Equal(2, summary.RecentFailures.Count);
        Assert.All(
            summary.RecentFailures,
            static failure =>
            {
                Assert.Equal(RadarProcessingAsyncFailureKind.None, failure.FailureKind);
                Assert.Equal(RadarProcessingAsyncCancellationKind.BeforeDispatch, failure.CancellationKind);
                Assert.NotNull(failure.WorkItemId);
            });
    }

    [Fact]
    public void RecorderSummarySnapshotIsStableAfterLaterMutationsAndCanReset()
    {
        var recorder = new RadarProcessingWorkerTelemetryRecorder();

        recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 1))));
        var first = recorder.CreateSummary();
        recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 2))));
        var second = recorder.CreateSummary();
        recorder.Reset();
        var reset = recorder.CreateSummary();

        Assert.Equal(1, first.Counters.DispatchedBatchCount);
        Assert.Single(first.RecentBatches);
        Assert.Equal(1, first.RecentBatches[0].BatchSequence);
        Assert.Equal(2, second.Counters.DispatchedBatchCount);
        Assert.Equal(2, second.RecentBatches.Count);
        Assert.Equal(0, reset.Counters.DispatchedBatchCount);
        Assert.Empty(reset.RecentBatches);
        Assert.Equal(0, reset.WorkerCount);
        Assert.Throws<ArgumentNullException>(() => recorder.RecordDispatch(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 3))), dispatchTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            recorder.RecordDispatch(CreateDispatch(CreateSucceededResult(CreatePlan(batchSequence: 4))), aggregationTime: TimeSpan.FromTicks(-1)));
    }
}
