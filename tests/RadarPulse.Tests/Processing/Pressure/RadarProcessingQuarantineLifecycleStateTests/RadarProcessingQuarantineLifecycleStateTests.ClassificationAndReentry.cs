using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleStateTests
{
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
}
