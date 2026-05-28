using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerLifecycleContractTests
{
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
