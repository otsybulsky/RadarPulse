using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCorePartitionedBarrierTests
{
    [Fact]
    public void PartitionedBarrierReturnsInvalidResultForUnsupportedStreamSchemaVersion()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>(),
            streamSchemaVersion: new StreamSchemaVersion(2));

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.UnsupportedStreamSchemaVersion, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void PartitionedBarrierReturnsInvalidResultForSourceUniverseVersionMismatch()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var batch = CreateBatch(
            new SourceUniverseVersion(2),
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceUniverseVersionMismatch, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }
}
