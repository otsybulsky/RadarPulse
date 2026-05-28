using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
    [Fact]
    public void WorkerGroupErrorEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingAsyncWorkerGroupError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncWorkerGroupError.AlreadyStarted);
        Assert.Equal(2, (int)RadarProcessingAsyncWorkerGroupError.NotStarted);
        Assert.Equal(3, (int)RadarProcessingAsyncWorkerGroupError.NotRunning);
        Assert.Equal(4, (int)RadarProcessingAsyncWorkerGroupError.Stopping);
        Assert.Equal(5, (int)RadarProcessingAsyncWorkerGroupError.Stopped);
        Assert.Equal(6, (int)RadarProcessingAsyncWorkerGroupError.Faulted);
        Assert.Equal(7, (int)RadarProcessingAsyncWorkerGroupError.Disposed);
        Assert.Equal(8, (int)RadarProcessingAsyncWorkerGroupError.AlreadyInFlight);
        Assert.Equal(9, (int)RadarProcessingAsyncWorkerGroupError.EnqueueRejected);
        Assert.Equal(10, (int)RadarProcessingAsyncWorkerGroupError.TimedOut);
        Assert.Equal(11, (int)RadarProcessingAsyncWorkerGroupError.ScopeClosed);
    }

    [Fact]
    public void WorkerGroupOptionsUseExecutionDefaults()
    {
        var options = RadarProcessingAsyncWorkerGroupOptions.Default;

        Assert.Same(RadarProcessingAsyncExecutionOptions.Default, options.Execution);
        Assert.Equal(RadarProcessingAsyncExecutionOptions.Default.WorkerCount, options.WorkerCount);
        Assert.Equal(RadarProcessingAsyncExecutionOptions.Default.QueueCapacity, options.QueueCapacity);
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncWorkerGroupResult(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(
                new RadarProcessingWorkerGroupStatus(),
                new RadarProcessingAsyncBatchScopeResult(
                    new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1)),
                (RadarProcessingAsyncWorkerGroupError)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(new RadarProcessingWorkerGroupStatus()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                new RadarProcessingWorkerGroupStatus(),
                RadarProcessingAsyncWorkerGroupError.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(
                new RadarProcessingWorkerGroupStatus(),
                new RadarProcessingAsyncBatchScopeResult(
                    new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1)),
                failureKind: (RadarProcessingAsyncFailureKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(
                new RadarProcessingWorkerGroupStatus(),
                new RadarProcessingAsyncBatchScopeResult(
                    new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1)),
                cancellationKind: (RadarProcessingAsyncCancellationKind)255));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                new RadarProcessingWorkerGroupStatus(
                    RadarProcessingWorkerGroupState.Running,
                    RadarProcessingWorkerHealth.Healthy,
                    workerCount: 1,
                    queueCapacity: 1),
                RadarProcessingAsyncWorkerGroupError.TimedOut));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                new RadarProcessingWorkerGroupStatus(),
                RadarProcessingAsyncWorkerGroupError.NotStarted,
                timeoutResult: new RadarProcessingAsyncTimeoutResult(
                    timedOut: true,
                    timeout: TimeSpan.FromMilliseconds(1),
                    timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(acceptedWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(completedWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(pendingWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(runningWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(barrierWaitTime: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void WorkerGroupExposesNoFireAndForgetBorrowedBatchDispatchApi()
    {
        var dispatchMethods = typeof(RadarProcessingAsyncWorkerGroup)
            .GetMethods()
            .Where(static method => method.Name.Contains("Dispatch", StringComparison.Ordinal))
            .ToArray();

        var dispatch = Assert.Single(dispatchMethods);
        Assert.Equal(nameof(RadarProcessingAsyncWorkerGroup.DispatchAsync), dispatch.Name);
        Assert.Equal(typeof(ValueTask<RadarProcessingAsyncWorkerGroupResult>), dispatch.ReturnType);
    }

}
