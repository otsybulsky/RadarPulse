using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerLifecycleContractTests
{
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
}
