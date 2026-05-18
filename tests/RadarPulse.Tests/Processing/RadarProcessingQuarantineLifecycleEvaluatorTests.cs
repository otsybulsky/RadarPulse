using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQuarantineLifecycleEvaluatorTests
{
    [Fact]
    public void EvaluatorUsesDefaultOptionsWhenNotSpecified()
    {
        var evaluator = new RadarProcessingQuarantineLifecycleEvaluator();

        Assert.Same(RadarProcessingQuarantineLifecycleOptions.Default, evaluator.Options);
    }

    [Theory]
    [InlineData(
        RadarProcessingHotPartitionClassification.None,
        RadarProcessingQuarantineEffectiveClassification.None,
        false)]
    [InlineData(
        RadarProcessingHotPartitionClassification.MovableHot,
        RadarProcessingQuarantineEffectiveClassification.MovableHot,
        false)]
    [InlineData(
        RadarProcessingHotPartitionClassification.IntrinsicHot,
        RadarProcessingQuarantineEffectiveClassification.IntrinsicHot,
        true)]
    public void NonQuarantineEvidenceRecordsCurrentEffectiveClassificationWithoutTransition(
        RadarProcessingHotPartitionClassification observedClassification,
        RadarProcessingQuarantineEffectiveClassification expectedClassification,
        bool expectedBlocksDirectMove)
    {
        var evaluator = new RadarProcessingQuarantineLifecycleEvaluator();
        var state = RadarProcessingQuarantineLifecycleState.Unclassified(partitionId: 1);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                shardId: 2,
                evaluationSequence: 5,
                pressure: 20,
                band: RadarProcessingPressureBand.Hot,
                observedClassification: observedClassification));

        Assert.False(result.HasTransition);
        Assert.Null(result.Transition);
        Assert.Equal(expectedClassification, result.State.EffectiveClassification);
        Assert.Equal(2, result.State.ShardId);
        Assert.Equal(5, result.State.LatestEvidenceSequence);
        Assert.Equal(new RadarProcessingPressureScore(20), result.State.LatestPressure);
        Assert.Equal(expectedBlocksDirectMove, result.State.BlocksDirectMove);
        Assert.False(result.State.HasQuarantineEvidence);
    }

    [Fact]
    public void QuarantinedEvidenceEntersQuarantineWithTransition()
    {
        var evaluator = new RadarProcessingQuarantineLifecycleEvaluator();
        var state = RadarProcessingQuarantineLifecycleState.Unclassified(partitionId: 1);

        var result = evaluator.Evaluate(
            state,
            CreateEvidence(
                partitionId: 1,
                shardId: 2,
                evaluationSequence: 10,
                pressure: 100,
                band: RadarProcessingPressureBand.SuperHot));

        Assert.True(result.HasTransition);
        Assert.True(result.State.IsQuarantined);
        Assert.True(result.State.BlocksDirectMove);
        Assert.Equal(10, result.State.QuarantineStartSequence);
        Assert.Equal(new RadarProcessingPressureScore(100), result.State.BaselinePressure);
        AssertTransition(
            result.Transition!,
            RadarProcessingQuarantineEffectiveClassification.None,
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
            RadarProcessingQuarantineTransitionReason.EnteredQuarantine,
            quarantineAgeEvaluations: 0);
    }

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

    private static RadarProcessingQuarantineLifecycleEvaluator CreateEvaluator(
        int quarantineTtlEvaluations,
        int sustainedCoolingSampleCount,
        double materialPressureChangeThreshold) =>
        new(
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations,
                sustainedCoolingSampleCount,
                materialPressureChangeThreshold));

    private static RadarProcessingQuarantineLifecycleState CreateQuarantinedState(
        int partitionId,
        int shardId = 0,
        long evaluationSequence = 0,
        double pressure = 100,
        RadarProcessingPressureBand band = RadarProcessingPressureBand.Hot) =>
        RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId)
            .EnterQuarantine(
                CreateEvidence(
                    partitionId,
                    shardId,
                    evaluationSequence,
                    pressure: pressure,
                    band: band));

    private static RadarProcessingQuarantineEvidence CreateEvidence(
        int partitionId = 0,
        int shardId = 0,
        long evaluationSequence = 0,
        RadarProcessingTopologyVersion? topologyVersion = null,
        double pressure = 1,
        RadarProcessingPressureBand band = RadarProcessingPressureBand.Hot,
        RadarProcessingHotPartitionClassification observedClassification =
            RadarProcessingHotPartitionClassification.Quarantined) =>
        new(
            partitionId,
            shardId,
            evaluationSequence,
            topologyVersion ?? RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(pressure),
            band,
            observedClassification);

    private static void AssertTransition(
        RadarProcessingQuarantineTransition transition,
        RadarProcessingQuarantineEffectiveClassification previousClassification,
        RadarProcessingQuarantineEffectiveClassification currentClassification,
        RadarProcessingQuarantineTransitionReason reason,
        long quarantineAgeEvaluations)
    {
        Assert.Equal(1, transition.PartitionId);
        Assert.Equal(previousClassification, transition.PreviousClassification);
        Assert.Equal(currentClassification, transition.CurrentClassification);
        Assert.Equal(reason, transition.Reason);
        Assert.Equal(quarantineAgeEvaluations, transition.QuarantineAgeEvaluations);
    }
}
