using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchScopeContractTests
{
    [Fact]
    public void CompletionFactoriesCarryWorkItemTimingAndMetrics()
    {
        var scope = new RadarProcessingAsyncBatchScope(3, new RadarProcessingTopologyVersion(2), 1);
        var item = scope.CreateWorkItem(0, new RadarProcessingWorkerId(1), shardId: 0, new[] { 0 });

        var completion = RadarProcessingAsyncWorkCompletion.Succeeded(
            item,
            queueWaitTime: TimeSpan.FromMilliseconds(2),
            executionTime: TimeSpan.FromMilliseconds(5),
            processedStreamEventCount: 7,
            processedPayloadValueCount: 11);

        Assert.Equal(item.BatchSequence, completion.BatchSequence);
        Assert.Equal(item.WorkItemId, completion.WorkItemId);
        Assert.Equal(item.TopologyVersion, completion.TopologyVersion);
        Assert.Equal(item.WorkerId, completion.WorkerId);
        Assert.Equal(RadarProcessingAsyncWorkStatus.Succeeded, completion.Status);
        Assert.True(completion.IsSuccessful);
        Assert.False(completion.IsFailed);
        Assert.False(completion.IsCanceled);
        Assert.Equal(TimeSpan.FromMilliseconds(2), completion.QueueWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(5), completion.ExecutionTime);
        Assert.Equal(7, completion.ProcessedStreamEventCount);
        Assert.Equal(11, completion.ProcessedPayloadValueCount);
        Assert.Equal(RadarProcessingAsyncFailureKind.None, completion.FailureKind);
        Assert.Equal(RadarProcessingAsyncCancellationKind.None, completion.CancellationKind);

        var failed = RadarProcessingAsyncWorkCompletion.Failed(
            item,
            failureKind: RadarProcessingAsyncFailureKind.WorkerException);
        var canceled = RadarProcessingAsyncWorkCompletion.Canceled(
            item,
            cancellationKind: RadarProcessingAsyncCancellationKind.WhileQueued);

        Assert.Equal(RadarProcessingAsyncFailureKind.WorkerException, failed.FailureKind);
        Assert.Equal(RadarProcessingAsyncCancellationKind.None, failed.CancellationKind);
        Assert.Equal(RadarProcessingAsyncFailureKind.None, canceled.FailureKind);
        Assert.Equal(RadarProcessingAsyncCancellationKind.WhileQueued, canceled.CancellationKind);
    }

    [Fact]
    public void CompletionRejectsInvalidShapes()
    {
        var topologyVersion = RadarProcessingTopologyVersion.Initial;
        var workerId = new RadarProcessingWorkerId(0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(-1, 0, topologyVersion, workerId, RadarProcessingAsyncWorkStatus.Succeeded));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(0, -1, topologyVersion, workerId, RadarProcessingAsyncWorkStatus.Succeeded));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(0, 0, topologyVersion, workerId, (RadarProcessingAsyncWorkStatus)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Failed,
                failureKind: (RadarProcessingAsyncFailureKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Canceled,
                cancellationKind: (RadarProcessingAsyncCancellationKind)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Failed));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                failureKind: RadarProcessingAsyncFailureKind.WorkerException));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                cancellationKind: RadarProcessingAsyncCancellationKind.WhileRunning));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                queueWaitTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                executionTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                processedStreamEventCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkCompletion(
                0,
                0,
                topologyVersion,
                workerId,
                RadarProcessingAsyncWorkStatus.Succeeded,
                processedPayloadValueCount: -1));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingAsyncWorkCompletion.Succeeded(null!));
    }
}
