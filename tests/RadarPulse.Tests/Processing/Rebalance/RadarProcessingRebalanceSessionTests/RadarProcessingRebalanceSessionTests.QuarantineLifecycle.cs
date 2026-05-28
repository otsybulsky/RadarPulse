using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceSessionTests
{
    [Fact]
    public void ActiveLifecycleQuarantineBlocksDirectMoveBeforePlanning()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var tracker = CreateLifecycleTracker(
            partitionCount: universe.SourceCount,
            quarantineTtlEvaluations: 10);
        tracker.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(4),
            RadarProcessingPressureBand.Warm,
            RadarProcessingHotPartitionClassification.Quarantined);
        var session = CreateSession(universe, quarantineLifecycleTracker: tracker);

        var result = session.Process(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1, 1, 1]));

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, result.DirectHotReliefDecision!.Kind);
        Assert.Equal(1, result.DirectHotReliefDecision.PartitionId);
        Assert.True(session.QuarantineLifecycleTracker.GetPartition(0).IsQuarantined);
        Assert.Empty(result.QuarantineTransitions);
    }

    [Fact]
    public void TtlRetryIsAdvancedBeforePlanningAndReported()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var tracker = CreateLifecycleTracker(
            partitionCount: universe.SourceCount,
            quarantineTtlEvaluations: 1);
        tracker.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(4),
            RadarProcessingPressureBand.Warm,
            RadarProcessingHotPartitionClassification.Quarantined);
        var session = CreateSession(universe, quarantineLifecycleTracker: tracker);

        var result = session.Process(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]));

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, result.DirectHotReliefDecision!.Kind);
        Assert.Equal(0, result.DirectHotReliefDecision.PartitionId);
        Assert.Contains(
            result.QuarantineTransitions,
            transition => transition.Reason == RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);
        Assert.Contains(
            result.QuarantineTransitions,
            transition => transition.Reason == RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief);
        Assert.Equal(1, result.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.QuarantineRetryCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.QuarantineClearCount);
        Assert.Equal(2, result.TelemetrySummary.RecentLifecycleTransitions.Count);
        Assert.Equal(
            RadarProcessingQuarantineEffectiveClassification.MovableHot,
            session.QuarantineLifecycleTracker.GetPartition(0).EffectiveClassification);
        Assert.False(session.QuarantineLifecycleTracker.GetPartition(0).HasQuarantineEvidence);
    }

    [Fact]
    public void RetryEligiblePartitionReentersQuarantineWhenRetryStillHasNoSafeTarget()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var tracker = CreateLifecycleTracker(
            partitionCount: universe.SourceCount,
            quarantineTtlEvaluations: 1);
        tracker.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingPressureScore(8),
            RadarProcessingPressureBand.Hot,
            RadarProcessingHotPartitionClassification.Quarantined);
        var session = CreateSession(universe, quarantineLifecycleTracker: tracker);

        var result = session.Process(CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 0, 0, 0, 0]));

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, result.DirectHotReliefDecision!.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            result.DirectHotReliefDecision.SkippedReasons);
        Assert.Contains(
            result.QuarantineTransitions,
            transition => transition.Reason == RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl);
        Assert.Contains(
            result.QuarantineTransitions,
            transition => transition.Reason == RadarProcessingQuarantineTransitionReason.ReenteredQuarantine);
        Assert.Equal(1, result.TelemetrySummary.Counters.RejectedCandidateCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.QuarantineRetryCount);
        Assert.Equal(1, result.TelemetrySummary.Counters.QuarantineReentryCount);
        Assert.Equal(2, result.TelemetrySummary.RecentLifecycleTransitions.Count);
        Assert.True(session.QuarantineLifecycleTracker.GetPartition(0).IsQuarantined);
        Assert.Equal(1, session.QuarantineLifecycleTracker.GetPartition(0).QuarantineStartSequence);
    }
}
