using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCoreSequentialTests
{
    [Fact]
    public void ProcessReturnsInvalidResultForSourceIdOutsideUniverse()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(
            universe.Version,
            new[] { CreateEvent(sourceId: 2, messageTimestampUtcTicks: 100, payloadOffset: 0) },
            new byte[] { 1, 2, 3, 4 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.Validation.Error);
        Assert.Equal(2, result.Validation.SourceId);
        Assert.Equal(0, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceOwnershipMismatch()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(
                    sourceId: 0,
                    messageTimestampUtcTicks: 100,
                    payloadOffset: 0,
                    azimuthBucket: 1)
            },
            new byte[] { 1, 2, 3, 4 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOwnershipMismatch, result.Validation.Error);
        Assert.Equal(0, result.Validation.SourceId);
        Assert.Equal(0, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceLocalTimestampRegression()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 0, messageTimestampUtcTicks: 99, payloadOffset: 4)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.Validation.Error);
        Assert.Equal(0, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(0, result.Metrics.ProcessedBatchCount);
        Assert.Equal(1, result.Metrics.ProcessedStreamEventCount);
        Assert.Equal(4, result.Metrics.ProcessedPayloadValueCount);
        Assert.Equal(10, result.Metrics.RawValueChecksum);
    }
}
