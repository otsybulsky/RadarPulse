using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingColdEvacuationPlannerTests
{
    [Fact]
    public void DirectHotReliefUnsafeAllowsColdEvacuation()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 4);
        classifier.ClassifyQuarantined(
            partitionId: 1,
            shardId: 0,
            evaluationSequence: 0);
        var directPlanner = new RadarProcessingDirectHotReliefPlanner();
        var coldPlanner = new RadarProcessingColdEvacuationPlanner();

        var directDecision = directPlanner.Plan(1, window, policyState, classifier);
        var coldDecision = coldPlanner.Plan(2, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, directDecision.Kind);
        Assert.True(classifier.GetPartition(0).IsIntrinsicHot);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, coldDecision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, coldDecision.MoveKind);
        Assert.Equal(1, coldDecision.PartitionId);
        Assert.Equal(0, coldDecision.SourceShardId);
        Assert.Equal(1, coldDecision.TargetShardId);
        Assert.Equal(1.0, coldDecision.ExpectedRelief);
        Assert.Equal(9.0, coldDecision.ProjectedPressure.SourceShardBefore.Value);
        Assert.Equal(8.0, coldDecision.ProjectedPressure.SourceShardAfter.Value);
        Assert.Equal(1.0, coldDecision.ProjectedPressure.TargetShardAfter.Value);
    }

    [Fact]
    public void LifecycleQuarantineDirectBlockStillAllowsColdEvacuationFallback()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 4);
        lifecycle.RecordEvidence(
            partitionId: 1,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(1),
            RadarProcessingPressureBand.Cold,
            RadarProcessingHotPartitionClassification.Quarantined);
        var directPlanner = new RadarProcessingDirectHotReliefPlanner();
        var coldPlanner = new RadarProcessingColdEvacuationPlanner();

        var directDecision = directPlanner.Plan(
            11,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);
        var coldDecision = coldPlanner.Plan(
            12,
            window,
            policyState,
            lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, directDecision.Kind);
        Assert.True(lifecycle.GetPartition(0).BlocksDirectMove);
        Assert.True(lifecycle.GetPartition(1).IsQuarantined);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, coldDecision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, coldDecision.MoveKind);
        Assert.Equal(1, coldDecision.PartitionId);
    }

    [Fact]
    public void SmallestUsefulColdPartitionIsSelectedDeterministically()
    {
        var window = CreateWindow(
            partitionCount: 6,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2],
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2]
            ]);
        var policyState = CreatePolicyState(partitionCount: 6, shardCount: 2);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(3, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, decision.MoveKind);
        Assert.Equal(2, decision.PartitionId);
        Assert.Equal(1.0, decision.ExpectedRelief);
    }

    [Fact]
    public void TargetShardRemainsBelowConfiguredHeadroomThreshold()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            targetHeadroomThreshold: 2.0);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(4, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(decision.ProjectedPressure.TargetShardAfter.Value <= policyState.Options.TargetHeadroomThreshold);
    }
}
