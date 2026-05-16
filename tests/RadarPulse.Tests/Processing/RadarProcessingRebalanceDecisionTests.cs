using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceDecisionTests
{
    [Fact]
    public void NoPressureReturnsNoActionWithNoSustainedPressure()
    {
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 1,
            evaluationSequence: 2,
            topologyVersion: RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 0,
            [RadarProcessingRebalanceSkippedReason.NoSustainedPressure]);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.False(decision.HasAcceptedMove);
        Assert.Null(decision.Candidate);
        Assert.Equal(RadarProcessingRebalanceMoveKind.None, decision.MoveKind);
        Assert.Null(decision.PartitionId);
        Assert.Equal(0, decision.PressureWindowSampleCount);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, decision.TopologyVersion);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.NoSustainedPressure,
            decision.SkippedReasons);
        Assert.Empty(decision.PolicyRejections);
    }

    [Fact]
    public void HotShardWithNoTargetRecordsSkippedTargetReason()
    {
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 3,
            evaluationSequence: 5,
            topologyVersion: new RadarProcessingTopologyVersion(7),
            pressureWindowSampleCount: 4,
            [
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
                RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget
            ]);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Equal(4, decision.PressureWindowSampleCount);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
            decision.SkippedReasons);
    }

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

    [Fact]
    public void AcceptedDecisionCarriesMoveTelemetry()
    {
        var topologyVersion = new RadarProcessingTopologyVersion(9);
        var resultTopologyVersion = topologyVersion.Next();
        var candidate = CreateCandidate(
            moveKind: RadarProcessingRebalanceMoveKind.DirectHotRelief,
            partitionId: 2,
            sourceShardId: 1,
            targetShardId: 3,
            expectedRelief: 4.5,
            sourceBefore: 12.0,
            targetBefore: 1.0,
            projectedSourceAfter: 7.5,
            projectedTargetAfter: 5.5);

        var decision = RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 10,
            evaluationSequence: 11,
            topologyVersion,
            pressureWindowSampleCount: 6,
            candidate,
            resultTopologyVersion);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(decision.HasAcceptedMove);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(2, decision.PartitionId);
        Assert.Equal(1, decision.SourceShardId);
        Assert.Equal(3, decision.TargetShardId);
        Assert.Equal(4.5, decision.ExpectedRelief);
        Assert.Equal(12.0, decision.ProjectedPressure.SourceShardBefore.Value);
        Assert.Equal(1.0, decision.ProjectedPressure.TargetShardBefore.Value);
        Assert.Equal(7.5, decision.ProjectedPressure.SourceShardAfter.Value);
        Assert.Equal(5.5, decision.ProjectedPressure.TargetShardAfter.Value);
        Assert.Equal(topologyVersion, decision.TopologyVersion);
        Assert.True(decision.ResultTopologyVersion.HasValue);
        Assert.Equal(resultTopologyVersion, decision.ResultTopologyVersion.Value);
        Assert.Empty(decision.SkippedReasons);
        Assert.Empty(decision.PolicyRejections);

        var request = candidate.ToTopologyMoveRequest(topologyVersion);

        Assert.Equal(topologyVersion, request.ExpectedTopologyVersion);
        Assert.Equal(candidate.PartitionId, request.PartitionId);
        Assert.Equal(candidate.SourceShardId, request.SourceShardId);
        Assert.Equal(candidate.TargetShardId, request.TargetShardId);
    }

    [Fact]
    public void DecisionIsDeterministicForSameInputs()
    {
        var candidate = CreateCandidate();

        var first = RadarProcessingRebalanceDecision.RejectedCandidate(
            decisionId: 12,
            evaluationSequence: 13,
            topologyVersion: new RadarProcessingTopologyVersion(14),
            pressureWindowSampleCount: 5,
            candidate,
            [
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
                RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot
            ]);
        var second = RadarProcessingRebalanceDecision.RejectedCandidate(
            decisionId: 12,
            evaluationSequence: 13,
            topologyVersion: new RadarProcessingTopologyVersion(14),
            pressureWindowSampleCount: 5,
            candidate,
            [
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard,
                RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot
            ]);

        Assert.Equal(first.DecisionId, second.DecisionId);
        Assert.Equal(first.EvaluationSequence, second.EvaluationSequence);
        Assert.Equal(first.TopologyVersion, second.TopologyVersion);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.MoveKind, second.MoveKind);
        Assert.Equal(first.PartitionId, second.PartitionId);
        Assert.Equal(first.SourceShardId, second.SourceShardId);
        Assert.Equal(first.TargetShardId, second.TargetShardId);
        Assert.Equal(first.ExpectedRelief, second.ExpectedRelief);
        Assert.Equal(first.SkippedReasons, second.SkippedReasons);
        Assert.Single(first.SkippedReasons, RadarProcessingRebalanceSkippedReason.NoColdTargetShard);
    }

    [Fact]
    public void DecisionCopiesSkippedReasons()
    {
        var reasons = new List<RadarProcessingRebalanceSkippedReason>
        {
            RadarProcessingRebalanceSkippedReason.NoHotShard
        };

        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 15,
            evaluationSequence: 16,
            topologyVersion: RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 2,
            reasons);

        reasons.Add(RadarProcessingRebalanceSkippedReason.NoColdTargetShard);

        Assert.Single(decision.SkippedReasons);
        Assert.Contains(RadarProcessingRebalanceSkippedReason.NoHotShard, decision.SkippedReasons);
    }

    [Fact]
    public void DecisionContractsRejectInvalidValues()
    {
        var candidate = CreateCandidate();
        var allowedPolicy = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            new RadarProcessingRebalanceOptions(
                minimumPartitionResidencyEvaluations: 0,
                minimumProjectedBenefit: 0.0))
            .EvaluateMove(candidate.ToPolicyInput());
        var mismatchedPolicy = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            new RadarProcessingRebalanceOptions(globalMoveBudgetPerWindow: 0))
            .EvaluateMove(CreateCandidate(partitionId: 1).ToPolicyInput());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRebalanceDecision.NoAction(
                decisionId: -1,
                evaluationSequence: 0,
                topologyVersion: RadarProcessingTopologyVersion.Initial,
                pressureWindowSampleCount: 0,
                [RadarProcessingRebalanceSkippedReason.NoSustainedPressure]));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceDecision.NoAction(
                decisionId: 1,
                evaluationSequence: 0,
                topologyVersion: RadarProcessingTopologyVersion.Initial,
                pressureWindowSampleCount: 0,
                []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRebalanceDecision.NoAction(
                decisionId: 1,
                evaluationSequence: 0,
                topologyVersion: RadarProcessingTopologyVersion.Initial,
                pressureWindowSampleCount: 0,
                [RadarProcessingRebalanceSkippedReason.None]));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId: 1,
                evaluationSequence: 0,
                topologyVersion: RadarProcessingTopologyVersion.Initial,
                pressureWindowSampleCount: 1,
                candidate,
                allowedPolicy));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceDecision.RejectedCandidate(
                decisionId: 1,
                evaluationSequence: 0,
                topologyVersion: RadarProcessingTopologyVersion.Initial,
                pressureWindowSampleCount: 1,
                candidate,
                mismatchedPolicy));
    }

    [Fact]
    public void CandidateRejectsInvalidMoveShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateCandidate(moveKind: RadarProcessingRebalanceMoveKind.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateCandidate(partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateCandidate(sourceShardId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateCandidate(targetShardId: -1));
        Assert.Throws<ArgumentException>(() =>
            CreateCandidate(sourceShardId: 1, targetShardId: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateCandidate(expectedRelief: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateCandidate(projectedTargetAfter: double.PositiveInfinity));
    }

    private static RadarProcessingRebalanceCandidate CreateCandidate(
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.ColdEvacuation,
        int partitionId = 0,
        int sourceShardId = 0,
        int targetShardId = 1,
        double expectedRelief = 1.0,
        double sourceBefore = 4.0,
        double targetBefore = 1.0,
        double projectedSourceAfter = 3.0,
        double projectedTargetAfter = 2.0) =>
        new(
            moveKind,
            partitionId,
            sourceShardId,
            targetShardId,
            new RadarProcessingProjectedPressure(
                new RadarProcessingPressureScore(sourceBefore),
                new RadarProcessingPressureScore(targetBefore),
                new RadarProcessingPressureScore(projectedSourceAfter),
                new RadarProcessingPressureScore(projectedTargetAfter)),
            expectedRelief);
}
