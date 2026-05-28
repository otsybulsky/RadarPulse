using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceDecisionTests
{
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
}
