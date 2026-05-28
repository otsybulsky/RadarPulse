using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleEvaluatorTests
{
    [Fact]
    public void InsufficientCoolingKeepsQuarantineActive()
    {
        var evaluator = CreateEvaluator(
            quarantineTtlEvaluations: 10,
            sustainedCoolingSampleCount: 2,
            materialPressureChangeThreshold: 1.0);
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 11,
                pressure: 90,
                band: RadarProcessingPressureBand.Normal));

        Assert.False(result.HasTransition);
        Assert.True(result.State.IsQuarantined);
        Assert.Equal(1, result.State.SustainedCoolingSampleCount);
        Assert.Equal(1, result.State.QuarantineAgeEvaluations);
    }

    [Fact]
    public void SustainedCoolingClearsQuarantine()
    {
        var evaluator = CreateEvaluator(
            quarantineTtlEvaluations: 10,
            sustainedCoolingSampleCount: 2,
            materialPressureChangeThreshold: 1.0);
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);
        var cooledOnce = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 11,
                pressure: 90,
                band: RadarProcessingPressureBand.Normal)).State;

        var result = evaluator.Evaluate(
            cooledOnce,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 12,
                pressure: 80,
                band: RadarProcessingPressureBand.Cold));

        Assert.True(result.HasTransition);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.None, result.State.EffectiveClassification);
        Assert.False(result.State.HasQuarantineEvidence);
        Assert.Equal(0, result.State.SustainedCoolingSampleCount);
        AssertTransition(
            result.Transition!,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineEffectiveClassification.None,
            RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling,
            quarantineAgeEvaluations: 2);
    }

    [Fact]
    public void HotSampleResetsCoolingAndPreservesQuarantine()
    {
        var evaluator = CreateEvaluator(
            quarantineTtlEvaluations: 10,
            sustainedCoolingSampleCount: 2,
            materialPressureChangeThreshold: 1.0);
        var state = CreateQuarantinedState(
            partitionId: 1,
            evaluationSequence: 10,
            pressure: 100);
        var cooledOnce = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 11,
                pressure: 90,
                band: RadarProcessingPressureBand.Normal)).State;

        var result = evaluator.Evaluate(
            cooledOnce,
            CreateEvidence(
                partitionId: 1,
                evaluationSequence: 12,
                pressure: 105,
                band: RadarProcessingPressureBand.Hot));

        Assert.False(result.HasTransition);
        Assert.True(result.State.IsQuarantined);
        Assert.Equal(0, result.State.SustainedCoolingSampleCount);
        Assert.Equal(new RadarProcessingPressureScore(105), result.State.LatestPressure);
    }
}
