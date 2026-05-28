using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    [Fact]
    public void ActiveLifecycleQuarantineIsNotSelectedForDirectMovement()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 4);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(4),
            RadarProcessingPressureBand.Hot,
            RadarProcessingHotPartitionClassification.Quarantined);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            33,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(1, decision.PartitionId);
        Assert.True(lifecycle.GetPartition(0).IsQuarantined);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.MovableHot, lifecycle.GetPartition(1).EffectiveClassification);
    }

    [Fact]
    public void ActiveLifecycleQuarantineReportsExplicitSkippedReasonWhenEveryHotPartitionIsBlocked()
    {
        var window = CreateWindow(
            partitionCount: 2,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0, 0, 0]
            ]);
        var policyState = CreatePolicyState(partitionCount: 2, shardCount: 2);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 2);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(8),
            RadarProcessingPressureBand.SuperHot,
            RadarProcessingHotPartitionClassification.Quarantined);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            34,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            decision.SkippedReasons);
        Assert.DoesNotContain(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

}
