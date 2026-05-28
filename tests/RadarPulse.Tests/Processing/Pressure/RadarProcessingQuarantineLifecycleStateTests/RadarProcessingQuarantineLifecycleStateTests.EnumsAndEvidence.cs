using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleStateTests
{
    [Fact]
    public void EffectiveClassificationEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingQuarantineEffectiveClassification.None);
        Assert.Equal(1, (int)RadarProcessingQuarantineEffectiveClassification.MovableHot);
        Assert.Equal(2, (int)RadarProcessingQuarantineEffectiveClassification.IntrinsicHot);
        Assert.Equal(3, (int)RadarProcessingQuarantineEffectiveClassification.Quarantined);
        Assert.Equal(4, (int)RadarProcessingQuarantineEffectiveClassification.RetryEligible);
    }

    [Fact]
    public void TransitionReasonEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingQuarantineTransitionReason.None);
        Assert.Equal(1, (int)RadarProcessingQuarantineTransitionReason.EnteredQuarantine);
        Assert.Equal(2, (int)RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);
        Assert.Equal(3, (int)RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleBySustainedCooling);
        Assert.Equal(4, (int)RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange);
        Assert.Equal(5, (int)RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling);
        Assert.Equal(6, (int)RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief);
        Assert.Equal(7, (int)RadarProcessingQuarantineTransitionReason.ClearedExplicitly);
        Assert.Equal(8, (int)RadarProcessingQuarantineTransitionReason.ReenteredQuarantine);
    }

    [Fact]
    public void UnclassifiedStateStartsWithoutQuarantineEvidence()
    {
        var state = RadarProcessingQuarantineLifecycleState.Unclassified(partitionId: 4);

        Assert.Equal(4, state.PartitionId);
        Assert.Equal(0, state.ShardId);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.None, state.EffectiveClassification);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, state.LatestTopologyVersion);
        Assert.Equal(0, state.LatestEvidenceSequence);
        Assert.False(state.HasQuarantineEvidence);
        Assert.Null(state.QuarantineStartSequence);
        Assert.Null(state.BaselinePressure);
        Assert.Equal(default, state.LatestPressure);
        Assert.Equal(RadarProcessingPressureBand.Cold, state.LatestPressureBand);
        Assert.Equal(0, state.SustainedCoolingSampleCount);
        Assert.False(state.BlocksDirectMove);
        Assert.False(state.IsRetryEligible);
        Assert.False(state.IsQuarantined);
        Assert.Equal(0, state.QuarantineAgeEvaluations);
    }

    [Fact]
    public void EvidenceCarriesCompactNumericState()
    {
        var evidence = CreateEvidence(
            partitionId: 2,
            shardId: 3,
            evaluationSequence: 5,
            topologyVersion: new RadarProcessingTopologyVersion(7),
            pressure: 42,
            band: RadarProcessingPressureBand.Hot,
            observedClassification: RadarProcessingHotPartitionClassification.Quarantined);

        Assert.Equal(2, evidence.PartitionId);
        Assert.Equal(3, evidence.ShardId);
        Assert.Equal(5, evidence.EvaluationSequence);
        Assert.Equal(new RadarProcessingTopologyVersion(7), evidence.TopologyVersion);
        Assert.Equal(new RadarProcessingPressureScore(42), evidence.PartitionPressure);
        Assert.Equal(RadarProcessingPressureBand.Hot, evidence.PartitionBand);
        Assert.Equal(RadarProcessingHotPartitionClassification.Quarantined, evidence.ObservedClassification);
    }

    [Fact]
    public void EvidenceRejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(shardId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(evaluationSequence: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(band: (RadarProcessingPressureBand)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateEvidence(observedClassification: (RadarProcessingHotPartitionClassification)255));
    }
}
