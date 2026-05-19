using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingWorkerLifecycleContractTests
{
    [Fact]
    public void WorkerLifecycleEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingWorkerGroupState.NotStarted);
        Assert.Equal(1, (int)RadarProcessingWorkerGroupState.Running);
        Assert.Equal(2, (int)RadarProcessingWorkerGroupState.Stopping);
        Assert.Equal(3, (int)RadarProcessingWorkerGroupState.Stopped);
        Assert.Equal(4, (int)RadarProcessingWorkerGroupState.Faulted);
        Assert.Equal(5, (int)RadarProcessingWorkerGroupState.Disposed);

        Assert.Equal(0, (int)RadarProcessingWorkerHealth.NotReady);
        Assert.Equal(1, (int)RadarProcessingWorkerHealth.Healthy);
        Assert.Equal(2, (int)RadarProcessingWorkerHealth.Draining);
        Assert.Equal(3, (int)RadarProcessingWorkerHealth.Faulted);
        Assert.Equal(4, (int)RadarProcessingWorkerHealth.Disposed);

        Assert.Equal(0, (int)RadarProcessingWorkerLifecycleError.None);
        Assert.Equal(1, (int)RadarProcessingWorkerLifecycleError.AlreadyStarted);
        Assert.Equal(2, (int)RadarProcessingWorkerLifecycleError.NotStarted);
        Assert.Equal(3, (int)RadarProcessingWorkerLifecycleError.NotRunning);
        Assert.Equal(4, (int)RadarProcessingWorkerLifecycleError.Stopping);
        Assert.Equal(5, (int)RadarProcessingWorkerLifecycleError.Stopped);
        Assert.Equal(6, (int)RadarProcessingWorkerLifecycleError.Faulted);
        Assert.Equal(7, (int)RadarProcessingWorkerLifecycleError.Disposed);
    }

    [Fact]
    public void WorkerIdRejectsNegativeValue()
    {
        var workerId = new RadarProcessingWorkerId(3);

        Assert.Equal(3, workerId.Value);
        Assert.Equal("3", workerId.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingWorkerId(-1));
    }

    [Fact]
    public void InitialLifecycleStatusUsesOptionsAndDoesNotAcceptDispatch()
    {
        var options = new RadarProcessingAsyncExecutionOptions(
            workerCount: 4,
            queueCapacity: 2);
        var lifecycle = new RadarProcessingWorkerGroupLifecycle(options);

        var status = lifecycle.Status;

        Assert.Equal(RadarProcessingWorkerGroupState.NotStarted, status.State);
        Assert.Equal(RadarProcessingWorkerHealth.NotReady, status.Health);
        Assert.Equal(4, status.WorkerCount);
        Assert.Equal(2, status.QueueCapacity);
        Assert.Equal(RadarProcessingWorkerLifecycleError.None, status.LastError);
        Assert.False(status.CanAcceptDispatch);
    }

    [Fact]
    public void LifecycleSupportsStartStopAcceptingStopAndDispose()
    {
        var lifecycle = new RadarProcessingWorkerGroupLifecycle();

        var started = lifecycle.Start();
        AssertSuccess(started, RadarProcessingWorkerGroupState.Running, RadarProcessingWorkerHealth.Healthy);
        Assert.True(started.Status.CanAcceptDispatch);

        var stopping = lifecycle.StopAccepting();
        AssertSuccess(stopping, RadarProcessingWorkerGroupState.Stopping, RadarProcessingWorkerHealth.Draining);
        Assert.False(stopping.Status.CanAcceptDispatch);

        var stopped = lifecycle.Stop();
        AssertSuccess(stopped, RadarProcessingWorkerGroupState.Stopped, RadarProcessingWorkerHealth.NotReady);

        var disposed = lifecycle.Dispose();
        AssertSuccess(disposed, RadarProcessingWorkerGroupState.Disposed, RadarProcessingWorkerHealth.Disposed);
    }

    [Fact]
    public void LifecycleRejectsInvalidTransitionsDeterministically()
    {
        var lifecycle = new RadarProcessingWorkerGroupLifecycle();

        AssertFailure(
            lifecycle.StopAccepting(),
            RadarProcessingWorkerLifecycleError.NotStarted,
            RadarProcessingWorkerGroupState.NotStarted);
        AssertFailure(
            lifecycle.Stop(),
            RadarProcessingWorkerLifecycleError.NotStarted,
            RadarProcessingWorkerGroupState.NotStarted);
        AssertFailure(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerLifecycleError.NotStarted,
            RadarProcessingWorkerGroupState.NotStarted);

        Assert.True(lifecycle.Start().IsSuccess);
        var duplicateStart = lifecycle.Start();
        AssertFailure(
            duplicateStart,
            RadarProcessingWorkerLifecycleError.AlreadyStarted,
            RadarProcessingWorkerGroupState.Running);
        Assert.True(duplicateStart.Status.CanAcceptDispatch);

        Assert.True(lifecycle.StopAccepting().IsSuccess);
        AssertFailure(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerLifecycleError.Stopping,
            RadarProcessingWorkerGroupState.Stopping);

        Assert.True(lifecycle.Stop().IsSuccess);
        AssertFailure(
            lifecycle.Start(),
            RadarProcessingWorkerLifecycleError.Stopped,
            RadarProcessingWorkerGroupState.Stopped);
    }

    [Fact]
    public void DispatchIsAllowedOnlyWhileRunningAndHealthy()
    {
        var lifecycle = new RadarProcessingWorkerGroupLifecycle();

        AssertFailure(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerLifecycleError.NotStarted,
            RadarProcessingWorkerGroupState.NotStarted);

        Assert.True(lifecycle.Start().IsSuccess);
        AssertSuccess(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerGroupState.Running,
            RadarProcessingWorkerHealth.Healthy);

        Assert.True(lifecycle.MarkFaulted().IsSuccess);
        AssertFailure(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerLifecycleError.Faulted,
            RadarProcessingWorkerGroupState.Faulted);

        Assert.True(lifecycle.Dispose().IsSuccess);
        AssertFailure(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerLifecycleError.Disposed,
            RadarProcessingWorkerGroupState.Disposed);
    }

    [Fact]
    public void FaultedLifecycleCarriesExplicitHealthAndError()
    {
        var lifecycle = new RadarProcessingWorkerGroupLifecycle();
        Assert.True(lifecycle.Start().IsSuccess);

        var faulted = lifecycle.MarkFaulted(RadarProcessingWorkerLifecycleError.NotRunning);

        Assert.True(faulted.IsSuccess);
        Assert.Equal(RadarProcessingWorkerGroupState.Faulted, faulted.Status.State);
        Assert.Equal(RadarProcessingWorkerHealth.Faulted, faulted.Status.Health);
        Assert.Equal(RadarProcessingWorkerLifecycleError.NotRunning, faulted.Status.LastError);
        Assert.False(faulted.Status.CanAcceptDispatch);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            lifecycle.MarkFaulted(RadarProcessingWorkerLifecycleError.None));
    }

    [Fact]
    public void DisposeIsIdempotentButDispatchRemainsRejected()
    {
        var lifecycle = new RadarProcessingWorkerGroupLifecycle();

        var first = lifecycle.Dispose();
        var second = lifecycle.Dispose();

        AssertSuccess(first, RadarProcessingWorkerGroupState.Disposed, RadarProcessingWorkerHealth.Disposed);
        AssertSuccess(second, RadarProcessingWorkerGroupState.Disposed, RadarProcessingWorkerHealth.Disposed);
        AssertFailure(
            lifecycle.ValidateDispatchAllowed(),
            RadarProcessingWorkerLifecycleError.Disposed,
            RadarProcessingWorkerGroupState.Disposed);
    }

    [Fact]
    public void StatusSnapshotIsImmutableAfterLaterTransitions()
    {
        var lifecycle = new RadarProcessingWorkerGroupLifecycle(
            new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 3));
        var before = lifecycle.Status;

        Assert.True(lifecycle.Start().IsSuccess);
        var after = lifecycle.Status;

        Assert.Equal(RadarProcessingWorkerGroupState.NotStarted, before.State);
        Assert.Equal(RadarProcessingWorkerHealth.NotReady, before.Health);
        Assert.Equal(2, before.WorkerCount);
        Assert.Equal(3, before.QueueCapacity);
        Assert.Equal(RadarProcessingWorkerGroupState.Running, after.State);
        Assert.Equal(RadarProcessingWorkerHealth.Healthy, after.Health);
    }

    [Fact]
    public void StatusRejectsInvalidShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupStatus(state: (RadarProcessingWorkerGroupState)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupStatus(health: (RadarProcessingWorkerHealth)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupStatus(lastError: (RadarProcessingWorkerLifecycleError)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupStatus(workerCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerGroupStatus(queueCapacity: -1));
    }

    [Fact]
    public void LifecycleResultRejectsInvalidShapes()
    {
        var status = new RadarProcessingWorkerGroupStatus();

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingWorkerLifecycleResult(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingWorkerLifecycleResult(status, (RadarProcessingWorkerLifecycleError)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingWorkerLifecycleResult.Failed(RadarProcessingWorkerLifecycleError.None, status));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingWorkerLifecycleResult.Failed(RadarProcessingWorkerLifecycleError.Disposed, status));
    }

    private static void AssertSuccess(
        RadarProcessingWorkerLifecycleResult result,
        RadarProcessingWorkerGroupState state,
        RadarProcessingWorkerHealth health)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(RadarProcessingWorkerLifecycleError.None, result.Error);
        Assert.Equal(state, result.Status.State);
        Assert.Equal(health, result.Status.Health);
    }

    private static void AssertFailure(
        RadarProcessingWorkerLifecycleResult result,
        RadarProcessingWorkerLifecycleError error,
        RadarProcessingWorkerGroupState state)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
        Assert.Equal(error, result.Status.LastError);
        Assert.Equal(state, result.Status.State);
    }
}
