using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    [Fact]
    public void CandidateIsRejectedWhenProjectedReliefIsTooSmall()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            minimumProjectedBenefit: 2.0);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(4, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(1.0, decision.ExpectedRelief);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit,
            decision.SkippedReasons);
    }

    [Fact]
    public void CandidateIsRejectedDuringCooldown()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            partitionMoveCooldownEvaluations: 2,
            globalMoveBudgetPerWindow: 4,
            sourceShardMoveBudgetPerWindow: 4,
            targetShardReceiveBudgetPerWindow: 4);
        policyState.RecordAcceptedMove(
            new RadarProcessingRebalanceMovePolicyInput(
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1,
                projectedBenefit: 4.0,
                targetProjectedPressure: new RadarProcessingPressureScore(4.0)));
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(5, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.PartitionInCooldown,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown,
            decision.SkippedReasons);
    }

    [Fact]
    public void AcceptedDirectMoveLowersProjectedMaxPressure()
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
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(6, window, policyState);
        var projectedMax = Math.Max(
            decision.ProjectedPressure.SourceShardAfter.Value,
            decision.ProjectedPressure.TargetShardAfter.Value);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(projectedMax < decision.ProjectedPressure.SourceShardBefore.Value);
    }

    [Fact]
    public void IneligibleWindowReturnsNoSustainedPressure()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(7, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.NoSustainedPressure,
            decision.SkippedReasons);
    }

    [Fact]
    public void EligibleWindowWithoutHotShardReturnsNoHotShard()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 1, 2],
                [0, 0, 1, 2]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(8, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.NoHotShard,
            decision.SkippedReasons);
    }

}
