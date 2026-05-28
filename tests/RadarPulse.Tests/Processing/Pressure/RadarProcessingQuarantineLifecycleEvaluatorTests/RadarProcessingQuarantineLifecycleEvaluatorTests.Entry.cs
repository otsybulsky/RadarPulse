using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQuarantineLifecycleEvaluatorTests
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
}
