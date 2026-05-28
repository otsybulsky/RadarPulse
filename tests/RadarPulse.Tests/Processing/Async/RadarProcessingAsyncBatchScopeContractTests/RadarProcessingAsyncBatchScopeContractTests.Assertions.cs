using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchScopeContractTests
{
    private static void AssertFailure(
        RadarProcessingAsyncBatchScopeResult result,
        RadarProcessingAsyncBatchCompletionError error)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }
}
