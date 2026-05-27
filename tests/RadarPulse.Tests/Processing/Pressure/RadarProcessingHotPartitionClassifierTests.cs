using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingHotPartitionClassifierTests
{
    [Fact]
    public void NewClassifierStartsWithUnclassifiedPartitions()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 3);

        for (var partitionId = 0; partitionId < 3; partitionId++)
        {
            var state = classifier.GetPartition(partitionId);

            Assert.Equal(partitionId, state.PartitionId);
            Assert.Equal(RadarProcessingHotPartitionClassification.None, state.Classification);
            Assert.False(state.BlocksDirectMove);
            Assert.Equal(0, state.IneffectiveMoveCount);
        }
    }

    [Fact]
    public void IntrinsicHotPartitionBlocksDirectMove()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);

        var state = classifier.ClassifyIntrinsicHot(
            partitionId: 1,
            shardId: 0,
            evaluationSequence: 4);

        Assert.True(state.IsIntrinsicHot);
        Assert.True(state.BlocksDirectMove);
        Assert.Equal(1, state.PartitionId);
        Assert.Equal(0, state.ShardId);
        Assert.Equal(4, state.EvaluationSequence);
        Assert.Equal(state, classifier.GetPartition(1));
    }

    [Fact]
    public void MovableHotPartitionDoesNotBlockDirectMove()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);

        var state = classifier.ClassifyMovableHot(
            partitionId: 0,
            shardId: 1,
            evaluationSequence: 5);

        Assert.True(state.IsMovableHot);
        Assert.False(state.BlocksDirectMove);
    }

    [Fact]
    public void RecentlyMovedIneffectivePartitionCanBecomeQuarantined()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(
            partitionCount: 2,
            quarantineIneffectiveMoveCount: 2,
            minimumEffectiveReliefRatio: 0.5);

        var first = classifier.RecordMoveOutcome(
            partitionId: 0,
            shardId: 1,
            evaluationSequence: 10,
            expectedRelief: 10.0,
            actualRelief: 1.0);
        var second = classifier.RecordMoveOutcome(
            partitionId: 0,
            shardId: 1,
            evaluationSequence: 11,
            expectedRelief: 10.0,
            actualRelief: 2.0);

        Assert.Equal(RadarProcessingHotPartitionClassification.MovableHot, first.Classification);
        Assert.Equal(1, first.IneffectiveMoveCount);
        Assert.True(second.IsQuarantined);
        Assert.True(second.BlocksDirectMove);
        Assert.Equal(2, second.IneffectiveMoveCount);
    }

    [Fact]
    public void EffectiveMoveOutcomeClearsIneffectiveCount()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(
            partitionCount: 2,
            quarantineIneffectiveMoveCount: 3,
            minimumEffectiveReliefRatio: 0.5);

        classifier.RecordMoveOutcome(
            partitionId: 0,
            shardId: 1,
            evaluationSequence: 10,
            expectedRelief: 10.0,
            actualRelief: 1.0);

        var effective = classifier.RecordMoveOutcome(
            partitionId: 0,
            shardId: 1,
            evaluationSequence: 11,
            expectedRelief: 10.0,
            actualRelief: 5.0);

        Assert.True(effective.IsMovableHot);
        Assert.Equal(0, effective.IneffectiveMoveCount);
        Assert.False(effective.BlocksDirectMove);
    }

    [Fact]
    public void ClearRemovesClassificationAndIneffectiveCount()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);

        classifier.ClassifyQuarantined(
            partitionId: 0,
            shardId: 1,
            evaluationSequence: 10);

        var cleared = classifier.Clear(
            partitionId: 0,
            evaluationSequence: 11);

        Assert.Equal(RadarProcessingHotPartitionClassification.None, cleared.Classification);
        Assert.Equal(0, cleared.IneffectiveMoveCount);
        Assert.False(cleared.BlocksDirectMove);
    }

    [Fact]
    public void ClassifierRejectsInvalidValues()
    {
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingHotPartitionClassifier(partitionCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingHotPartitionClassifier(partitionCount: 1, quarantineIneffectiveMoveCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingHotPartitionClassifier(partitionCount: 1, minimumEffectiveReliefRatio: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            classifier.GetPartition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            classifier.ClassifyMovableHot(partitionId: 2, shardId: 0, evaluationSequence: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            classifier.ClassifyIntrinsicHot(partitionId: 0, shardId: -1, evaluationSequence: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            classifier.ClassifyQuarantined(partitionId: 0, shardId: 0, evaluationSequence: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            classifier.RecordMoveOutcome(
                partitionId: 0,
                shardId: 0,
                evaluationSequence: 0,
                expectedRelief: double.PositiveInfinity,
                actualRelief: 0.0));
    }
}
