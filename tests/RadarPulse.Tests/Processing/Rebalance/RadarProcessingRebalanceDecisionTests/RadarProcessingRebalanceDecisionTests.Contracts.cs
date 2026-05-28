using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceDecisionTests
{
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
}
