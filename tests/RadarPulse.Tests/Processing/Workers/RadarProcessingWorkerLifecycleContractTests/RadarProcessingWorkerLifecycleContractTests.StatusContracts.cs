using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerLifecycleContractTests
{
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
}
