using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    [Fact]
    public void IntrinsicHotPartitionIsNotSelectedForDirectMovement()
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
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 4);
        classifier.ClassifyIntrinsicHot(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(31, window, policyState, classifier);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(1, decision.PartitionId);
        Assert.Equal(0, decision.SourceShardId);
        Assert.Equal(1, decision.TargetShardId);
    }

    [Fact]
    public void ClassifiedPartitionsReturnDiagnosticNoActionWhenEveryHotPartitionIsBlocked()
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
        classifier.ClassifyIntrinsicHot(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(32, window, policyState, classifier);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

}
