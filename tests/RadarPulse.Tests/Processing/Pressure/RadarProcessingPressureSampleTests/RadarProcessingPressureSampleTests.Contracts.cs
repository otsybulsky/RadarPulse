using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPressureSampleTests
{
    [Fact]
    public void PressureOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureScore(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureScore(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureScore(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureOptions(eventWeight: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingPressureOptions(eventWeight: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(payloadValueWeight: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(coldThreshold: 10, warmThreshold: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(warmThreshold: 10, hotThreshold: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureOptions(hotThreshold: 10, superHotThreshold: 9));
    }
}
