using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryContractTests
{
    [Fact]
    public void RecentAcceptedMoveCarriesProjectedPressure()
    {
        var pressure = CreateProjectedPressure();

        var move = new RadarProcessingRebalanceRecentAcceptedMove(
            decisionId: 1,
            evaluationSequence: 2,
            RadarProcessingTopologyVersion.Initial,
            new RadarProcessingTopologyVersion(2),
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            partitionId: 3,
            sourceShardId: 4,
            targetShardId: 5,
            pressure,
            expectedRelief: 6.5);

        Assert.Equal(1, move.DecisionId);
        Assert.Equal(2, move.EvaluationSequence);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, move.TopologyVersion);
        Assert.Equal(new RadarProcessingTopologyVersion(2), move.ResultTopologyVersion);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, move.MoveKind);
        Assert.Equal(3, move.PartitionId);
        Assert.Equal(4, move.SourceShardId);
        Assert.Equal(5, move.TargetShardId);
        Assert.Equal(pressure, move.ProjectedPressure);
        Assert.Equal(6.5, move.ExpectedRelief);
    }

    [Fact]
    public void RecentAcceptedMoveRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(decisionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(moveKind: RadarProcessingRebalanceMoveKind.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(moveKind: (RadarProcessingRebalanceMoveKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(partitionId: -1));
        Assert.Throws<ArgumentException>(() =>
            CreateAcceptedMove(sourceShardId: 1, targetShardId: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(expectedRelief: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAcceptedMove(expectedRelief: -1.0));
    }

    [Fact]
    public void RecentAcceptedMoveCanBeProjectedFromAcceptedDecision()
    {
        var candidate = new RadarProcessingRebalanceCandidate(
            RadarProcessingRebalanceMoveKind.ColdEvacuation,
            partitionId: 1,
            sourceShardId: 2,
            targetShardId: 3,
            CreateProjectedPressure(),
            expectedRelief: 4.0);
        var decision = RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 5,
            evaluationSequence: 6,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 7,
            candidate,
            resultTopologyVersion: new RadarProcessingTopologyVersion(2));

        var recent = RadarProcessingRebalanceRecentAcceptedMove.FromDecision(decision);

        Assert.Equal(decision.DecisionId, recent.DecisionId);
        Assert.Equal(decision.EvaluationSequence, recent.EvaluationSequence);
        Assert.Equal(decision.TopologyVersion, recent.TopologyVersion);
        Assert.Equal(decision.ResultTopologyVersion, recent.ResultTopologyVersion);
        Assert.Equal(candidate.MoveKind, recent.MoveKind);
        Assert.Equal(candidate.PartitionId, recent.PartitionId);
        Assert.Equal(candidate.ExpectedRelief, recent.ExpectedRelief);
    }

    [Fact]
    public void RecentAcceptedMoveRejectsNonAcceptedDecisionProjection()
    {
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 1,
            evaluationSequence: 1,
            RadarProcessingTopologyVersion.Initial,
            pressureWindowSampleCount: 1,
            new[] { RadarProcessingRebalanceSkippedReason.NoHotShard });

        Assert.Throws<ArgumentException>(() =>
            RadarProcessingRebalanceRecentAcceptedMove.FromDecision(decision));
    }

}
