using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    [Fact]
    public void RetryEligibleLifecyclePartitionCanBeReconsideredForDirectMovement()
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
        Advance(policyState, 2);
        var lifecycle = CreateRetryEligibleLifecycle(
            partitionCount: 4,
            partitionId: 0,
            shardId: 0,
            pressure: 4,
            band: RadarProcessingPressureBand.Hot,
            topologyVersion: window.LatestTopologyVersion);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            35,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.MovableHot, lifecycle.GetPartition(0).EffectiveClassification);
        Assert.False(lifecycle.GetPartition(0).HasQuarantineEvidence);
    }

    [Fact]
    public void RetryEligibleLifecyclePartitionReentersQuarantineWhenNoSafeTargetExists()
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
        Advance(policyState, 2);
        var lifecycle = CreateRetryEligibleLifecycle(
            partitionCount: 2,
            partitionId: 0,
            shardId: 0,
            pressure: 8,
            band: RadarProcessingPressureBand.SuperHot,
            topologyVersion: window.LatestTopologyVersion);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            36,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.True(lifecycle.GetPartition(0).IsQuarantined);
        Assert.Equal(2, lifecycle.GetPartition(0).QuarantineStartSequence);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            decision.SkippedReasons);
        Assert.DoesNotContain(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

    [Fact]
    public void ClearedLifecycleQuarantineDoesNotReportStaleSkippedReason()
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
        Advance(policyState, 3);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(
            partitionCount: 4,
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations: 10,
                sustainedCoolingSampleCount: 2,
                materialPressureChangeThreshold: 1.0));
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(4),
            RadarProcessingPressureBand.Hot,
            RadarProcessingHotPartitionClassification.Quarantined);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 1,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(1),
            RadarProcessingPressureBand.Normal,
            RadarProcessingHotPartitionClassification.None);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 2,
            window.LatestTopologyVersion,
            RadarProcessingPressureScore.Zero,
            RadarProcessingPressureBand.Cold,
            RadarProcessingHotPartitionClassification.None);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            37,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.DoesNotContain(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            decision.SkippedReasons);
    }

}
