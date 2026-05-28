using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorderTests
{
    [Fact]
    public void RecorderAggregatesNoActionDecisionAndSkippedReasons()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 1,
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 3,
            new[]
            {
                RadarProcessingRebalanceSkippedReason.NoHotShard,
                RadarProcessingRebalanceSkippedReason.NoColdTargetShard
            });

        recorder.RecordDecision(decision);
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.EvaluationCount);
        Assert.Equal(1, summary.Counters.NoActionDecisionCount);
        Assert.Equal(0, summary.Counters.AcceptedMoveCount);
        Assert.Equal(2, summary.SkippedReasonCounters.Count);
        Assert.Equal(RadarProcessingRebalanceSkippedReason.NoHotShard, summary.SkippedReasonCounters[0].Reason);
        Assert.Equal(1, summary.SkippedReasonCounters[0].Count);
        Assert.Equal(RadarProcessingRebalanceSkippedReason.NoColdTargetShard, summary.SkippedReasonCounters[1].Reason);
        Assert.Single(summary.RecentDecisions);
        Assert.Equal(decision.DecisionId, summary.RecentDecisions[0].DecisionId);
        Assert.Empty(summary.RecentAcceptedMoves);
    }

    [Fact]
    public void RecorderAggregatesAcceptedMoveKindsAndRecentMoves()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();
        var direct = CreateAcceptedDecision(
            decisionId: 1,
            RadarProcessingRebalanceMoveKind.DirectHotRelief);
        var cold = CreateAcceptedDecision(
            decisionId: 2,
            RadarProcessingRebalanceMoveKind.ColdEvacuation);

        recorder.RecordDecision(direct);
        recorder.RecordDecision(cold);
        var summary = recorder.CreateSummary();

        Assert.Equal(2, summary.Counters.EvaluationCount);
        Assert.Equal(2, summary.Counters.AcceptedMoveCount);
        Assert.Equal(1, summary.Counters.DirectHotReliefMoveCount);
        Assert.Equal(1, summary.Counters.ColdEvacuationMoveCount);
        Assert.Equal(2, summary.RecentDecisions.Count);
        Assert.Equal(2, summary.RecentAcceptedMoves.Count);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, summary.RecentAcceptedMoves[0].MoveKind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, summary.RecentAcceptedMoves[1].MoveKind);
    }

    [Fact]
    public void RecorderAggregatesRejectedCandidateAndPolicySkippedReason()
    {
        var recorder = new RadarProcessingRebalanceTelemetryRecorder();
        var candidate = CreateCandidate();
        var decision = RadarProcessingRebalanceDecision.RejectedCandidate(
            decisionId: 3,
            evaluationSequence: 4,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 5,
            candidate,
            new[] { RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded },
            new[] { RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded });

        recorder.RecordDecision(decision);
        var summary = recorder.CreateSummary();

        Assert.Equal(1, summary.Counters.EvaluationCount);
        Assert.Equal(1, summary.Counters.RejectedCandidateCount);
        Assert.Equal(0, summary.Counters.AcceptedMoveCount);
        Assert.Single(summary.SkippedReasonCounters);
        Assert.Equal(RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded, summary.SkippedReasonCounters[0].Reason);
        Assert.Single(summary.RecentDecisions);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, summary.RecentDecisions[0].Kind);
        Assert.Equal(RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded, summary.RecentDecisions[0].PolicyRejections[0]);
    }
}
