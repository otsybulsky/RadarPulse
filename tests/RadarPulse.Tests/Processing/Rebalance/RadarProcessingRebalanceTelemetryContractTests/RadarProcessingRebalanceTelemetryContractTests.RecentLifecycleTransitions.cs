using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void RecentLifecycleTransitionCarriesCompactTransitionState()
    {
        var transition = new RadarProcessingRebalanceRecentLifecycleTransition(
            partitionId: 1,
            shardId: 2,
            evaluationSequence: 3,
            new RadarProcessingTopologyVersion(4),
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
            new RadarProcessingPressureScore(5),
            quarantineAgeEvaluations: 6);

        Assert.Equal(1, transition.PartitionId);
        Assert.Equal(2, transition.ShardId);
        Assert.Equal(3, transition.EvaluationSequence);
        Assert.Equal(new RadarProcessingTopologyVersion(4), transition.TopologyVersion);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.Quarantined, transition.PreviousClassification);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.RetryEligible, transition.CurrentClassification);
        Assert.Equal(RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl, transition.Reason);
        Assert.Equal(new RadarProcessingPressureScore(5), transition.Pressure);
        Assert.Equal(6, transition.QuarantineAgeEvaluations);
    }

    [Fact]
    public void RecentLifecycleTransitionRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(shardId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(evaluationSequence: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(previousClassification: (RadarProcessingQuarantineEffectiveClassification)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(currentClassification: (RadarProcessingQuarantineEffectiveClassification)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(reason: RadarProcessingQuarantineTransitionReason.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(reason: (RadarProcessingQuarantineTransitionReason)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleTransition(quarantineAgeEvaluations: -1));
    }

    [Fact]
    public void RecentLifecycleTransitionCanBeProjectedFromTransition()
    {
        var transition = new RadarProcessingQuarantineTransition(
            partitionId: 1,
            shardId: 2,
            evaluationSequence: 3,
            new RadarProcessingTopologyVersion(4),
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange,
            new RadarProcessingPressureScore(5),
            quarantineAgeEvaluations: 6);

        var recent = RadarProcessingRebalanceRecentLifecycleTransition.FromTransition(transition);

        Assert.Equal(transition.PartitionId, recent.PartitionId);
        Assert.Equal(transition.ShardId, recent.ShardId);
        Assert.Equal(transition.EvaluationSequence, recent.EvaluationSequence);
        Assert.Equal(transition.TopologyVersion, recent.TopologyVersion);
        Assert.Equal(transition.Reason, recent.Reason);
        Assert.Equal(transition.QuarantineAgeEvaluations, recent.QuarantineAgeEvaluations);
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRebalanceRecentLifecycleTransition.FromTransition(null!));
    }

}
