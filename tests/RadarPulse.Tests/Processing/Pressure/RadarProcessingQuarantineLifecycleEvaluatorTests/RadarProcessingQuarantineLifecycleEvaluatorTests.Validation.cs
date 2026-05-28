using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleEvaluatorTests
{
    [Fact]
    public void RetryEligiblePartitionCanClearToObservedEffectiveClassification()
    {
        var evaluator = new RadarProcessingQuarantineLifecycleEvaluator();
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100)
            .MarkRetryEligible(
                CreateEvidence(
                    partitionId: 1,
                    evaluationSequence: 20,
                    pressure: 70,
                    band: RadarProcessingPressureBand.Warm),
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 21,
                pressure: 60,
                band: RadarProcessingPressureBand.Hot,
                observedClassification: RadarProcessingHotPartitionClassification.IntrinsicHot));

        Assert.True(result.HasTransition);
        Assert.False(result.State.HasQuarantineEvidence);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.IntrinsicHot, result.State.EffectiveClassification);
        Assert.True(result.State.BlocksDirectMove);
        AssertTransition(
            result.Transition!,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineEffectiveClassification.IntrinsicHot,
            RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief,
            quarantineAgeEvaluations: 11);
    }

    [Fact]
    public void EvaluationResultRejectsInconsistentTransition()
    {
        var state = RadarProcessingQuarantineLifecycleState.Unclassified(partitionId: 1);
        var transition = new RadarProcessingQuarantineTransition(
            partitionId: 2,
            shardId: 0,
            evaluationSequence: 0,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.None,
            RadarProcessingQuarantineTransitionReason.ClearedExplicitly,
            new RadarProcessingPressureScore(1),
            quarantineAgeEvaluations: 0);

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQuarantineLifecycleEvaluationResult(null!));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingQuarantineLifecycleEvaluationResult(state, transition));
    }

    [Fact]
    public void EvaluatorRejectsInvalidInputs()
    {
        var evaluator = new RadarProcessingQuarantineLifecycleEvaluator();
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);

        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(null!, CreateEvidence(partitionId: 1, evaluationSequence: 11)));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(state, null!));
        Assert.Throws<ArgumentException>(() =>
            evaluator.Evaluate(state, CreateEvidence(partitionId: 2, evaluationSequence: 11)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            evaluator.Evaluate(state, CreateEvidence(partitionId: 1, evaluationSequence: 9)));
    }
}
