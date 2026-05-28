using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceDecisionTests
{
    [Fact]
    public void RejectedCandidateFromPolicyResultRecordsAllRelevantReasons()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            new RadarProcessingRebalanceOptions(
                budgetWindowEvaluationCount: 10,
                globalMoveBudgetPerWindow: 0,
                sourceShardMoveBudgetPerWindow: 0,
                targetShardReceiveBudgetPerWindow: 0,
                minimumPartitionResidencyEvaluations: 2,
                minimumProjectedBenefit: 2.0,
                targetHeadroomThreshold: 5.0));
        var candidate = CreateCandidate(
            expectedRelief: 1.0,
            projectedTargetAfter: 6.0);
        var policyResult = state.EvaluateMove(candidate.ToPolicyInput());

        var decision = RadarProcessingRebalanceDecision.RejectedCandidate(
            decisionId: 4,
            evaluationSequence: state.EvaluationSequence,
            topologyVersion: RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            candidate,
            policyResult);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.False(decision.HasAcceptedMove);
        Assert.Equal(candidate, decision.Candidate);
        Assert.Equal(candidate.PartitionId, decision.PartitionId);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.PartitionBelowMinimumResidency,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.TargetShardReceiveBudgetExhausted,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.CandidatePartitionBelowMinimumResidency,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetShardReceiveBudgetExhausted,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded,
            decision.SkippedReasons);
    }
}
