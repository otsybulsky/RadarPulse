using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQuarantineLifecycleStateTests
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

    [Fact]
    public void EnterQuarantineRecordsStartSequenceAndBaselinePressure()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(
                partitionId: 1,
                shardId: 2,
                evaluationSequence: 10,
                pressure: 100,
                band: RadarProcessingPressureBand.SuperHot,
                observedClassification: RadarProcessingHotPartitionClassification.Quarantined));

        Assert.Equal(1, state.PartitionId);
        Assert.Equal(2, state.ShardId);
        Assert.True(state.IsQuarantined);
        Assert.True(state.BlocksDirectMove);
        Assert.True(state.HasQuarantineEvidence);
        Assert.Equal(10, state.QuarantineStartSequence);
        Assert.Equal(10, state.LatestEvidenceSequence);
        Assert.Equal(new RadarProcessingPressureScore(100), state.BaselinePressure);
        Assert.Equal(new RadarProcessingPressureScore(100), state.LatestPressure);
        Assert.Equal(RadarProcessingPressureBand.SuperHot, state.LatestPressureBand);
        Assert.Equal(0, state.SustainedCoolingSampleCount);
        Assert.Equal(0, state.QuarantineAgeEvaluations);
    }

    [Fact]
    public void CoolingSamplesIncrementAndHotSamplesResetCoolingCount()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(partitionId: 1, evaluationSequence: 10, pressure: 100));

        var cooledOnce = state.RecordCoolingSample(CreateEvidence(
            partitionId: 1,
            evaluationSequence: 11,
            pressure: 40,
            band: RadarProcessingPressureBand.Normal));
        var cooledTwice = cooledOnce.RecordCoolingSample(CreateEvidence(
            partitionId: 1,
            evaluationSequence: 12,
            pressure: 30,
            band: RadarProcessingPressureBand.Cold));
        var hotAgain = cooledTwice.RecordHotSample(CreateEvidence(
            partitionId: 1,
            evaluationSequence: 13,
            pressure: 110,
            band: RadarProcessingPressureBand.Hot));

        Assert.Equal(1, cooledOnce.SustainedCoolingSampleCount);
        Assert.Equal(2, cooledTwice.SustainedCoolingSampleCount);
        Assert.Equal(new RadarProcessingPressureScore(30), cooledTwice.LatestPressure);
        Assert.Equal(RadarProcessingPressureBand.Cold, cooledTwice.LatestPressureBand);
        Assert.Equal(0, hotAgain.SustainedCoolingSampleCount);
        Assert.Equal(new RadarProcessingPressureScore(110), hotAgain.LatestPressure);
        Assert.Equal(3, hotAgain.QuarantineAgeEvaluations);
    }

    [Fact]
    public void RetryEligibilityPreservesQuarantineEvidence()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(partitionId: 1, evaluationSequence: 10, pressure: 100))
            .MarkRetryEligible(
                CreateEvidence(partitionId: 1, evaluationSequence: 20, pressure: 70),
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);

        Assert.True(state.IsRetryEligible);
        Assert.False(state.IsQuarantined);
        Assert.False(state.BlocksDirectMove);
        Assert.True(state.HasQuarantineEvidence);
        Assert.Equal(10, state.QuarantineStartSequence);
        Assert.Equal(new RadarProcessingPressureScore(100), state.BaselinePressure);
        Assert.Equal(new RadarProcessingPressureScore(70), state.LatestPressure);
        Assert.Equal(10, state.QuarantineAgeEvaluations);
    }

    [Fact]
    public void ClearRemovesQuarantineEvidence()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(partitionId: 1, evaluationSequence: 10, pressure: 100))
            .RecordCoolingSample(CreateEvidence(partitionId: 1, evaluationSequence: 11, pressure: 20))
            .Clear(
                CreateEvidence(partitionId: 1, evaluationSequence: 12, pressure: 10),
                RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling);

        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.None, state.EffectiveClassification);
        Assert.False(state.HasQuarantineEvidence);
        Assert.Null(state.QuarantineStartSequence);
        Assert.Null(state.BaselinePressure);
        Assert.Equal(0, state.SustainedCoolingSampleCount);
        Assert.Equal(new RadarProcessingPressureScore(10), state.LatestPressure);
        Assert.Equal(0, state.QuarantineAgeEvaluations);
    }

    [Fact]
    public void RecordClassificationEvidenceUpdatesNonQuarantineClassification()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .RecordClassificationEvidence(
                CreateEvidence(
                    partitionId: 1,
                    shardId: 2,
                    evaluationSequence: 10,
                    pressure: 50,
                    observedClassification: RadarProcessingHotPartitionClassification.IntrinsicHot),
                RadarProcessingQuarantineEffectiveClassification.IntrinsicHot);

        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.IntrinsicHot, state.EffectiveClassification);
        Assert.True(state.BlocksDirectMove);
        Assert.False(state.HasQuarantineEvidence);
        Assert.Equal(2, state.ShardId);
        Assert.Equal(10, state.LatestEvidenceSequence);
        Assert.Equal(new RadarProcessingPressureScore(50), state.LatestPressure);
    }

    [Fact]
    public void ClearToClassificationRemovesQuarantineEvidenceAndKeepsObservedClassification()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(partitionId: 1, evaluationSequence: 10, pressure: 100))
            .ClearToClassification(
                CreateEvidence(
                    partitionId: 1,
                    evaluationSequence: 20,
                    pressure: 70,
                    observedClassification: RadarProcessingHotPartitionClassification.MovableHot),
                RadarProcessingQuarantineEffectiveClassification.MovableHot,
                RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief);

        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.MovableHot, state.EffectiveClassification);
        Assert.False(state.BlocksDirectMove);
        Assert.False(state.HasQuarantineEvidence);
        Assert.Null(state.QuarantineStartSequence);
        Assert.Null(state.BaselinePressure);
        Assert.Equal(0, state.SustainedCoolingSampleCount);
    }

    [Fact]
    public void ReenterQuarantineOverwritesStaleEvidence()
    {
        var state = RadarProcessingQuarantineLifecycleState
            .Unclassified(partitionId: 1)
            .EnterQuarantine(CreateEvidence(partitionId: 1, shardId: 2, evaluationSequence: 10, pressure: 100))
            .MarkRetryEligible(
                CreateEvidence(partitionId: 1, shardId: 2, evaluationSequence: 20, pressure: 50),
                RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange)
            .ReenterQuarantine(CreateEvidence(
                partitionId: 1,
                shardId: 3,
                evaluationSequence: 21,
                pressure: 120,
                band: RadarProcessingPressureBand.SuperHot));

        Assert.True(state.IsQuarantined);
        Assert.Equal(3, state.ShardId);
        Assert.Equal(21, state.QuarantineStartSequence);
        Assert.Equal(new RadarProcessingPressureScore(120), state.BaselinePressure);
        Assert.Equal(0, state.SustainedCoolingSampleCount);
        Assert.Equal(0, state.QuarantineAgeEvaluations);
    }

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

    private static RadarProcessingQuarantineEvidence CreateEvidence(
        int partitionId = 0,
        int shardId = 0,
        long evaluationSequence = 0,
        RadarProcessingTopologyVersion? topologyVersion = null,
        double pressure = 1,
        RadarProcessingPressureBand band = RadarProcessingPressureBand.Hot,
        RadarProcessingHotPartitionClassification observedClassification = RadarProcessingHotPartitionClassification.Quarantined) =>
        new(
            partitionId,
            shardId,
            evaluationSequence,
            topologyVersion ?? RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(pressure),
            band,
            observedClassification);

    private static RadarProcessingQuarantineTransition CreateTransition(
        int partitionId = 0,
        int shardId = 0,
        long evaluationSequence = 0,
        RadarProcessingQuarantineEffectiveClassification previousClassification =
            RadarProcessingQuarantineEffectiveClassification.Quarantined,
        RadarProcessingQuarantineEffectiveClassification currentClassification =
            RadarProcessingQuarantineEffectiveClassification.RetryEligible,
        RadarProcessingQuarantineTransitionReason reason =
            RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl,
        long quarantineAgeEvaluations = 0) =>
        new(
            partitionId,
            shardId,
            evaluationSequence,
            RadarProcessingTopologyVersion.Initial,
            previousClassification,
            currentClassification,
            reason,
            new RadarProcessingPressureScore(1),
            quarantineAgeEvaluations);
}
