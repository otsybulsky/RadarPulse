using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQuarantineLifecycleTrackerTests
{
    [Fact]
    public void TrackerStartsWithUnclassifiedPartitions()
    {
        var tracker = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 3);

        Assert.Equal(3, tracker.PartitionCount);
        Assert.Same(RadarProcessingQuarantineLifecycleOptions.Default, tracker.Options);
        Assert.Equal(3, tracker.Partitions.Count);

        for (var partitionId = 0; partitionId < tracker.PartitionCount; partitionId++)
        {
            var state = tracker.GetPartition(partitionId);

            Assert.Equal(partitionId, state.PartitionId);
            Assert.Equal(RadarProcessingQuarantineEffectiveClassification.None, state.EffectiveClassification);
            Assert.False(state.HasQuarantineEvidence);
        }
    }

    [Fact]
    public void RecordEvidenceUpdatesPartitionStateAndReturnsTransition()
    {
        var tracker = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 2);

        var result = tracker.RecordEvidence(
            partitionId: 1,
            shardId: 2,
            evaluationSequence: 10,
            new RadarProcessingTopologyVersion(3),
            new RadarProcessingPressureScore(100),
            RadarProcessingPressureBand.SuperHot,
            RadarProcessingHotPartitionClassification.Quarantined);

        Assert.True(result.HasTransition);
        Assert.True(result.State.IsQuarantined);
        Assert.Equal(result.State, tracker.GetPartition(1));
        Assert.Equal(new RadarProcessingTopologyVersion(3), tracker.GetPartition(1).LatestTopologyVersion);
        Assert.Equal(RadarProcessingQuarantineTransitionReason.EnteredQuarantine, result.Transition!.Reason);
    }

    [Fact]
    public void RecordPartitionEvidenceUsesCompactPressureState()
    {
        var tracker = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 2);
        var partition = new RadarProcessingPartitionPressureState(
            partitionId: 1,
            shardId: 2,
            sampleCount: 3,
            RadarProcessingRouteMetrics.Empty,
            new RadarProcessingPressureScore(9),
            RadarProcessingPressureBand.Hot);

        var result = tracker.RecordPartitionEvidence(
            partition,
            evaluationSequence: 5,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingHotPartitionClassification.MovableHot);

        Assert.False(result.HasTransition);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.MovableHot, result.State.EffectiveClassification);
        Assert.Equal(2, result.State.ShardId);
        Assert.Equal(new RadarProcessingPressureScore(9), result.State.LatestPressure);
    }

    [Fact]
    public void TrackerRejectsInvalidValues()
    {
        var tracker = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQuarantineLifecycleTracker(partitionCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tracker.GetPartition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tracker.GetPartition(2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tracker.RecordEvidence(
                partitionId: 2,
                shardId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                new RadarProcessingPressureScore(1),
                RadarProcessingPressureBand.Hot,
                RadarProcessingHotPartitionClassification.MovableHot));
    }
}
