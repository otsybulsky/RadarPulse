using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void RecentDecisionCopiesReasonCollections()
    {
        var skippedReasons = new List<RadarProcessingRebalanceSkippedReason>
        {
            RadarProcessingRebalanceSkippedReason.NoHotShard
        };
        var policyRejections = new List<RadarProcessingRebalancePolicyRejection>
        {
            RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted
        };

        var decision = new RadarProcessingRebalanceRecentDecision(
            decisionId: 3,
            evaluationSequence: 5,
            RadarProcessingTopologyVersion.Initial,
            RadarProcessingRebalanceDecisionKind.RejectedCandidate,
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            partitionId: 1,
            sourceShardId: 2,
            targetShardId: 3,
            skippedReasons,
            policyRejections);

        skippedReasons.Add(RadarProcessingRebalanceSkippedReason.NoColdTargetShard);
        policyRejections.Add(RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded);

        Assert.Equal(3, decision.DecisionId);
        Assert.Equal(5, decision.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, decision.TopologyVersion);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(1, decision.PartitionId);
        Assert.Equal(2, decision.SourceShardId);
        Assert.Equal(3, decision.TargetShardId);
        Assert.Equal(new[] { RadarProcessingRebalanceSkippedReason.NoHotShard }, decision.SkippedReasons);
        Assert.Equal(new[] { RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted }, decision.PolicyRejections);
    }

    [Fact]
    public void RecentDecisionRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: -1,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: -1,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                (RadarProcessingRebalanceDecisionKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.AcceptedMove,
                (RadarProcessingRebalanceMoveKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.AcceptedMove,
                partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction,
                skippedReasons: new[] { RadarProcessingRebalanceSkippedReason.None }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceRecentDecision(
                decisionId: 0,
                evaluationSequence: 0,
                RadarProcessingTopologyVersion.Initial,
                RadarProcessingRebalanceDecisionKind.NoAction,
                policyRejections: new[] { RadarProcessingRebalancePolicyRejection.None }));
    }

    [Fact]
    public void RecentDecisionCanBeProjectedFromDecision()
    {
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 11,
            evaluationSequence: 12,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            new[] { RadarProcessingRebalanceSkippedReason.NoHotShard });

        var recent = RadarProcessingRebalanceRecentDecision.FromDecision(decision);

        Assert.Equal(decision.DecisionId, recent.DecisionId);
        Assert.Equal(decision.EvaluationSequence, recent.EvaluationSequence);
        Assert.Equal(decision.TopologyVersion, recent.TopologyVersion);
        Assert.Equal(decision.Kind, recent.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.None, recent.MoveKind);
        Assert.Null(recent.PartitionId);
        Assert.Equal(decision.SkippedReasons, recent.SkippedReasons);
    }

}
