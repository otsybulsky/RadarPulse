using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleEvaluatorTests
{
    [Fact]
    public void TtlExpiryMarksQuarantineRetryEligible()
    {
        var evaluator = CreateEvaluator(
            quarantineTtlEvaluations: 2,
            sustainedCoolingSampleCount: 5,
            materialPressureChangeThreshold: 1.0);
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 12,
                pressure: 100,
                band: RadarProcessingPressureBand.Hot));

        Assert.True(result.HasTransition);
        Assert.True(result.State.IsRetryEligible);
        Assert.False(result.State.BlocksDirectMove);
        Assert.Equal(10, result.State.QuarantineStartSequence);
        AssertTransition(
            result.Transition!,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
            quarantineAgeEvaluations: 2);
    }

    [Theory]
    [InlineData(70)]
    [InlineData(130)]
    public void MaterialPressureChangeMarksQuarantineRetryEligible(
        double latestPressure)
    {
        var evaluator = CreateEvaluator(
            quarantineTtlEvaluations: 10,
            sustainedCoolingSampleCount: 5,
            materialPressureChangeThreshold: 0.25);
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 11,
                pressure: latestPressure,
                band: RadarProcessingPressureBand.Warm));

        Assert.True(result.HasTransition);
        Assert.True(result.State.IsRetryEligible);
        AssertTransition(
            result.Transition!,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange,
            quarantineAgeEvaluations: 1);
    }

    [Fact]
    public void ImmaterialPressureChangeKeepsQuarantineActive()
    {
        var evaluator = CreateEvaluator(
            quarantineTtlEvaluations: 10,
            sustainedCoolingSampleCount: 5,
            materialPressureChangeThreshold: 0.5);
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 11,
                pressure: 70,
                band: RadarProcessingPressureBand.Warm));

        Assert.False(result.HasTransition);
        Assert.True(result.State.IsQuarantined);
        Assert.Equal(1, result.State.QuarantineAgeEvaluations);
    }

    [Fact]
    public void RetryEligiblePartitionCanReenterQuarantineWithFreshEvidence()
    {
        var evaluator = new RadarProcessingQuarantineLifecycleEvaluator();
        var state = CreateQuarantinedState(
            partitionId: 1,
            shardId: 2,
            evaluationSequence: 10,
            pressure: 100)
            .MarkRetryEligible(
                CreateEvidence(
                    partitionId: 1,
                    shardId: 2,
                    evaluationSequence: 20,
                    pressure: 70,
                    band: RadarProcessingPressureBand.Warm),
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                shardId: 3,
                evaluationSequence: 21,
                pressure: 120,
                band: RadarProcessingPressureBand.SuperHot));

        Assert.True(result.HasTransition);
        Assert.True(result.State.IsQuarantined);
        Assert.Equal(21, result.State.QuarantineStartSequence);
        Assert.Equal(new RadarProcessingPressureScore(120), result.State.BaselinePressure);
        Assert.Equal(0, result.State.QuarantineAgeEvaluations);
        AssertTransition(
            result.Transition!,
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineTransitionReason.ReenteredQuarantine,
            quarantineAgeEvaluations: 0);
    }
}
