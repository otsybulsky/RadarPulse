using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingColdEvacuationPlannerTests
{
    [Fact]
    public void ColdEvacuationIsRejectedWhenProjectedReliefIsTooSmall()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            minimumProjectedBenefit: 2.0);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(5, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, decision.MoveKind);
        Assert.Equal(1.0, decision.ExpectedRelief);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit,
            decision.SkippedReasons);
    }

    [Fact]
    public void ColdEvacuationIsRejectedWhenTargetWouldBecomeWarm()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]
            ],
            warmEnterThreshold: 2.0);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(6, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, decision.MoveKind);
        Assert.Equal(1, decision.PartitionId);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm,
            decision.SkippedReasons);
    }

    [Fact]
    public void SourceShardMoveBudgetCapsRepeatedColdEvacuation()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            globalMoveBudgetPerWindow: 4,
            sourceShardMoveBudgetPerWindow: 1,
            targetShardReceiveBudgetPerWindow: 4);
        policyState.RecordAcceptedMove(
            new RadarProcessingRebalanceMovePolicyInput(
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 1,
                projectedBenefit: 1.0,
                targetProjectedPressure: new RadarProcessingPressureScore(1.0)));
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(7, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted,
            decision.SkippedReasons);
    }
}
