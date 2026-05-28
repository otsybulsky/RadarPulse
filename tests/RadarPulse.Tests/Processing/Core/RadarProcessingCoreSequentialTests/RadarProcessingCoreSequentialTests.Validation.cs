using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCoreSequentialTests
{
    [Fact]
    public void ProcessHonorsCancellationBeforeProcessing()
    {
        var core = new RadarProcessingCore(CreateUniverse(sourceCount: 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            core.Process(CreateEmptyBatch(core.Topology.SourceUniverseVersion), cancellation.Token));
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
    }

    [Fact]
    public void ProcessReturnsInvalidResultForUnsupportedStreamSchemaVersion()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateEmptyBatch(
            universe.Version,
            streamSchemaVersion: new StreamSchemaVersion(2));

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.UnsupportedStreamSchemaVersion, result.Validation.Error);
        Assert.Equal(-1, result.Validation.EventIndex);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }

    [Fact]
    public void ProcessReturnsInvalidResultForSourceUniverseVersionMismatch()
    {
        var universe = CreateUniverse(sourceCount: 1, version: SourceUniverseVersion.Initial);
        var core = new RadarProcessingCore(universe);
        var batch = CreateEmptyBatch(new SourceUniverseVersion(2));

        var result = core.Process(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceUniverseVersionMismatch, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
    }
}
