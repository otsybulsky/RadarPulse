using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerLifecycleContractTests
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
}
