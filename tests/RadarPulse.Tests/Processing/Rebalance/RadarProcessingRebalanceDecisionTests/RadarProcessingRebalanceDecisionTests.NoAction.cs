using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceDecisionTests
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
}
