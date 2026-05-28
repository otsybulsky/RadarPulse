using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    [Fact]
    public void SustainedHotShardProducesDirectReliefCandidate()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(1, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(decision.HasAcceptedMove);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Equal(0, decision.SourceShardId);
        Assert.Equal(1, decision.TargetShardId);
        Assert.Equal(4.0, decision.ExpectedRelief);
        Assert.Equal(8.0, decision.ProjectedPressure.SourceShardBefore.Value);
        Assert.Equal(0.0, decision.ProjectedPressure.TargetShardBefore.Value);
        Assert.Equal(4.0, decision.ProjectedPressure.SourceShardAfter.Value);
        Assert.Equal(4.0, decision.ProjectedPressure.TargetShardAfter.Value);
        Assert.Empty(decision.SkippedReasons);
        Assert.Empty(decision.PolicyRejections);
    }

    [Fact]
    public void LargestUsefulPartitionIsSelectedDeterministically()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 1, 1, 1],
                [0, 0, 0, 0, 0, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(2, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Equal(5.0, decision.ProjectedPressure.TargetShardAfter.Value);
        Assert.Equal(3.0, decision.ExpectedRelief);
    }

    [Fact]
    public void CandidateIsRejectedWhenEveryTargetWouldBecomeHot()
    {
        var window = CreateWindow(
            partitionCount: 2,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0, 0, 0]
            ]);
        var policyState = CreatePolicyState(partitionCount: 2, shardCount: 2);
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(3, window, policyState, classifier);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(0, decision.PartitionId);
        Assert.True(classifier.GetPartition(0).IsIntrinsicHot);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

}
