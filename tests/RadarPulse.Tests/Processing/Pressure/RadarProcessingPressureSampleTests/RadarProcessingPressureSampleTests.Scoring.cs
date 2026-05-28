using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPressureSampleTests
{
    [Fact]
    public void PressureScoreIncreasesWithEventCount()
    {
        var options = new RadarProcessingPressureOptions(
            eventWeight: 2.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0);

        var lower = options.Score(new RadarProcessingRouteMetrics(1, payloadValueCount: 10, rawValueChecksum: 10));
        var higher = options.Score(new RadarProcessingRouteMetrics(2, payloadValueCount: 10, rawValueChecksum: 10));

        Assert.True(higher.Value > lower.Value);
        Assert.Equal(2.0, lower.Value);
        Assert.Equal(4.0, higher.Value);
    }

    [Fact]
    public void PressureScoreIncreasesWithPayloadValueCount()
    {
        var options = new RadarProcessingPressureOptions(
            eventWeight: 0.0,
            payloadValueWeight: 0.5,
            rawValueChecksumWeight: 0.0);

        var lower = options.Score(new RadarProcessingRouteMetrics(eventCount: 1, payloadValueCount: 4, rawValueChecksum: 10));
        var higher = options.Score(new RadarProcessingRouteMetrics(eventCount: 1, payloadValueCount: 8, rawValueChecksum: 10));

        Assert.True(higher.Value > lower.Value);
        Assert.Equal(2.0, lower.Value);
        Assert.Equal(4.0, higher.Value);
    }

    [Fact]
    public void PressureBandClassificationIsDeterministic()
    {
        var options = new RadarProcessingPressureOptions(
            eventWeight: 1.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0,
            coldThreshold: 0.0,
            warmThreshold: 10.0,
            hotThreshold: 50.0,
            superHotThreshold: 100.0);

        Assert.Equal(RadarProcessingPressureBand.Cold, options.Classify(new RadarProcessingPressureScore(0)));
        Assert.Equal(RadarProcessingPressureBand.Normal, options.Classify(new RadarProcessingPressureScore(5)));
        Assert.Equal(RadarProcessingPressureBand.Warm, options.Classify(new RadarProcessingPressureScore(10)));
        Assert.Equal(RadarProcessingPressureBand.Hot, options.Classify(new RadarProcessingPressureScore(50)));
        Assert.Equal(RadarProcessingPressureBand.SuperHot, options.Classify(new RadarProcessingPressureScore(100)));
    }
}
