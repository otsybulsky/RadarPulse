using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleStateTests
{
    [Fact]
    public void LifecycleStateRejectsMismatchedOrOutOfOrderEvidence()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(partitionId: 1, evaluationSequence: 10));

        Assert.Throws<ArgumentException>(() =>
            state.RecordCoolingSample(CreateEvidence(partitionId: 2, evaluationSequence: 11)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.RecordCoolingSample(CreateEvidence(partitionId: 1, evaluationSequence: 9)));
        Assert.Throws<ArgumentNullException>(() =>
            state.RecordCoolingSample(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.MarkRetryEligible(
                CreateEvidence(partitionId: 1, evaluationSequence: 11),
                RadarProcessingQuarantineTransitionReason.ClearedExplicitly));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.Clear(
                CreateEvidence(partitionId: 1, evaluationSequence: 11),
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.RecordClassificationEvidence(
                CreateEvidence(partitionId: 1, evaluationSequence: 11),
                RadarProcessingQuarantineEffectiveClassification.Quarantined));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.ClearToClassification(
                CreateEvidence(partitionId: 1, evaluationSequence: 11),
                RadarProcessingQuarantineEffectiveClassification.RetryEligible,
                RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief));
    }

    [Fact]
    public void TransitionCarriesLifecycleTelemetry()
    {
        var transition = new RadarProcessingQuarantineTransition(
            partitionId: 1,
            shardId: 2,
            evaluationSequence: 30,
            new RadarProcessingTopologyVersion(4),
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
            new RadarProcessingPressureScore(55),
            quarantineAgeEvaluations: 20);

        Assert.Equal(1, transition.PartitionId);
        Assert.Equal(2, transition.ShardId);
        Assert.Equal(30, transition.EvaluationSequence);
        Assert.Equal(new RadarProcessingTopologyVersion(4), transition.TopologyVersion);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.Quarantined, transition.PreviousClassification);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.RetryEligible, transition.CurrentClassification);
        Assert.Equal(RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl, transition.Reason);
        Assert.Equal(new RadarProcessingPressureScore(55), transition.Pressure);
        Assert.Equal(20, transition.QuarantineAgeEvaluations);
    }

    [Fact]
    public void TransitionRejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransition(partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransition(shardId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransition(evaluationSequence: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateTransition(previousClassification: (RadarProcessingQuarantineEffectiveClassification)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateTransition(currentClassification: (RadarProcessingQuarantineEffectiveClassification)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateTransition(reason: RadarProcessingQuarantineTransitionReason.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateTransition(reason: (RadarProcessingQuarantineTransitionReason)255));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransition(quarantineAgeEvaluations: -1));
    }
}
