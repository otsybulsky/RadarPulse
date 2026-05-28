using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceDecisionTests
{
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
}
