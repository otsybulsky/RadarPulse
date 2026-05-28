using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    private static void AssertInvalid(
        RadarProcessingAsyncValidationResult result,
        RadarProcessingAsyncValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }
}
