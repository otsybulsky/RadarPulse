using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceValidatorTests
{
    private static void AssertInvalid(
        RadarProcessingRebalanceValidationResult result,
        RadarProcessingRebalanceValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }
}
