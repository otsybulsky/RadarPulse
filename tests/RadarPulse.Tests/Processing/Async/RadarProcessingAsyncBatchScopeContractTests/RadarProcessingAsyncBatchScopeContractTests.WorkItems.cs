using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchScopeContractTests
{
    [Fact]
    public void AsyncBatchCompletionEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingAsyncWorkStatus.Succeeded);
        Assert.Equal(2, (int)RadarProcessingAsyncWorkStatus.Failed);
        Assert.Equal(3, (int)RadarProcessingAsyncWorkStatus.Canceled);

        Assert.Equal(0, (int)RadarProcessingAsyncFailureKind.None);
        Assert.Equal(1, (int)RadarProcessingAsyncFailureKind.WorkerReportedFailure);
        Assert.Equal(2, (int)RadarProcessingAsyncFailureKind.WorkerException);
        Assert.Equal(3, (int)RadarProcessingAsyncFailureKind.DispatchRejected);
        Assert.Equal(4, (int)RadarProcessingAsyncFailureKind.EnqueueRejected);
        Assert.Equal(5, (int)RadarProcessingAsyncFailureKind.TimedOut);
        Assert.Equal(6, (int)RadarProcessingAsyncFailureKind.WorkerGroupFaulted);

        Assert.Equal(0, (int)RadarProcessingAsyncCancellationKind.None);
        Assert.Equal(1, (int)RadarProcessingAsyncCancellationKind.BeforeDispatch);
        Assert.Equal(2, (int)RadarProcessingAsyncCancellationKind.WhileQueued);
        Assert.Equal(3, (int)RadarProcessingAsyncCancellationKind.WhileRunning);
        Assert.Equal(4, (int)RadarProcessingAsyncCancellationKind.Timeout);
        Assert.Equal(5, (int)RadarProcessingAsyncCancellationKind.Mixed);

        Assert.Equal(0, (int)RadarProcessingAsyncBatchCompletionError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncBatchCompletionError.ScopeMismatch);
        Assert.Equal(2, (int)RadarProcessingAsyncBatchCompletionError.TopologyVersionMismatch);
        Assert.Equal(3, (int)RadarProcessingAsyncBatchCompletionError.WorkItemOutOfRange);
        Assert.Equal(4, (int)RadarProcessingAsyncBatchCompletionError.DuplicateCompletion);
        Assert.Equal(5, (int)RadarProcessingAsyncBatchCompletionError.MissingCompletion);
        Assert.Equal(6, (int)RadarProcessingAsyncBatchCompletionError.ScopeClosed);
        Assert.Equal(7, (int)RadarProcessingAsyncBatchCompletionError.WorkFailed);
        Assert.Equal(8, (int)RadarProcessingAsyncBatchCompletionError.WorkCanceled);
    }

    [Fact]
    public void ScopeCreatesWorkItemWithCopiedPartitionIds()
    {
        var scope = new RadarProcessingAsyncBatchScope(7, new RadarProcessingTopologyVersion(3), 2);
        var partitionIds = new[] { 0, 2, 4 };

        var item = scope.CreateWorkItem(
            workItemId: 1,
            new RadarProcessingWorkerId(5),
            shardId: 2,
            partitionIds);
        partitionIds[0] = 99;

        Assert.Equal(7, item.BatchSequence);
        Assert.Equal(1, item.WorkItemId);
        Assert.Equal(new RadarProcessingTopologyVersion(3), item.TopologyVersion);
        Assert.Equal(5, item.WorkerId.Value);
        Assert.Equal(2, item.ShardId);
        Assert.Equal(new[] { 0, 2, 4 }, item.PartitionIds);
        Assert.Equal(3, item.PartitionCount);
    }

    [Fact]
    public void WorkItemRejectsInvalidShapes()
    {
        var topologyVersion = RadarProcessingTopologyVersion.Initial;
        var workerId = new RadarProcessingWorkerId(0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(-1, 0, topologyVersion, workerId, 0, new[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(0, -1, topologyVersion, workerId, 0, new[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, -1, new[] { 0 }));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, null!));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, Array.Empty<int>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, new[] { -1 }));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, new[] { 1, 1 }));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkItem(0, 0, topologyVersion, workerId, 0, new[] { 2, 1 }));
    }
}
