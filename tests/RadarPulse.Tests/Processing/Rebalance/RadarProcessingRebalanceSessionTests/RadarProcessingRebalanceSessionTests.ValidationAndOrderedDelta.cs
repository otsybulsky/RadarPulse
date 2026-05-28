using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceSessionTests
{
    [Fact]
    public void InvalidProcessingResultDoesNotEvaluateRebalance()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = CreateSession(universe);

        var result = session.Process(CreateEmptyBatch(new SourceUniverseVersion(2)));

        Assert.False(result.ProcessingResult.IsValid);
        Assert.True(result.Validation.IsValid);
        Assert.Null(result.PressureSample);
        Assert.Null(result.DirectHotReliefDecision);
        Assert.Null(result.ColdEvacuationDecision);
        Assert.Null(result.RebalanceDecision);
        Assert.Null(result.MigrationResult);
        Assert.Empty(result.QuarantineTransitions);
        Assert.Equal(0, result.TelemetrySummary.Counters.EvaluationCount);
        Assert.Empty(result.TelemetrySummary.RecentDecisions);
        Assert.Equal(0, session.PressureWindow.SampleCount);
        Assert.Equal(0, session.PolicyState.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, session.CurrentTopology.Version);
    }

    [Fact]
    public void SessionResultReportsSelectedValidationProfile()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = CreateSession(
            universe,
            hardeningOptions: new RadarProcessingRebalanceHardeningOptions(
                validationProfile: RadarProcessingValidationProfile.Off));

        var result = session.Process(CreateEmptyBatch(universe.Version));

        Assert.Equal(RadarProcessingValidationProfile.Off, session.ValidationProfile);
        Assert.Equal(RadarProcessingValidationProfile.Off, result.ValidationProfile);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(0, result.TelemetrySummary.Counters.ValidationFailureCount);
    }

    [Fact]
    public void OrderedDeltaCommitPreservesAcceptedMoveAgainstDirectProcess()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var direct = CreateSession(universe);
        var ordered = CreateSession(universe);
        var batch = CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]);

        var directResult = direct.Process(batch);
        using var delta = ordered.Core.ComputeProcessingDelta(batch);
        var orderedResult = ordered.CommitProcessingDelta(delta);

        Assert.True(orderedResult.ProcessingResult.IsValid);
        Assert.True(orderedResult.Validation.IsValid);
        Assert.Equal(directResult.PublishedMigration, orderedResult.PublishedMigration);
        Assert.Equal(direct.CurrentTopology.Version, ordered.CurrentTopology.Version);
        Assert.Equal(directResult.RebalanceDecision?.Kind, orderedResult.RebalanceDecision?.Kind);
        Assert.Equal(directResult.RebalanceDecision?.MoveKind, orderedResult.RebalanceDecision?.MoveKind);
        Assert.Equal(
            directResult.TelemetrySummary.Counters.AcceptedMoveCount,
            orderedResult.TelemetrySummary.Counters.AcceptedMoveCount);
    }

    [Fact]
    public void OrderedDeltaCommitValidationFailureDoesNotEvaluateRebalance()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var session = CreateSession(universe, shardCount: 1);
        var first = session.Process(CreateEightBitBatch(
            universe.Version,
            [0],
            messageTimestampBase: 200));
        using var staleTimestampDelta = session.Core.ComputeProcessingDelta(
            CreateEightBitBatch(
                universe.Version,
                [0],
                messageTimestampBase: 100));

        var result = session.CommitProcessingDelta(staleTimestampDelta);

        Assert.True(first.Validation.IsValid);
        Assert.False(result.ProcessingResult.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.ProcessingResult.Validation.Error);
        Assert.Null(result.PressureSample);
        Assert.Null(result.DirectHotReliefDecision);
        Assert.Null(result.ColdEvacuationDecision);
        Assert.False(result.PublishedMigration);
        Assert.Equal(1, session.PressureWindow.SampleCount);
        Assert.Equal(1, session.PolicyState.EvaluationSequence);
    }

    [Fact]
    public void SequentialCoreIsRejectedForRebalanceSession()
    {
        var core = new RadarProcessingCore(CreateUniverse(sourceCount: 1));

        Assert.Throws<ArgumentException>(() => new RadarProcessingRebalanceSession(core));
    }
}
