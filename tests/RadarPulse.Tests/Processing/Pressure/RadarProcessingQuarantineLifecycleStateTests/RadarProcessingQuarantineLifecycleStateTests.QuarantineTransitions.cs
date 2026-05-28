using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleStateTests
{
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
}
