using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
    [Fact]
    public void PartitionCannotMoveBeforeMinimumResidency()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(minimumPartitionResidencyEvaluations: 2));

        var early = state.EvaluateMove(CreateInput());

        Assert.False(early.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.PartitionBelowMinimumResidency,
            early.Rejections);

        state.AdvanceEvaluation();
        state.AdvanceEvaluation();

        var ready = state.EvaluateMove(CreateInput());

        Assert.True(ready.IsAllowed);
        Assert.Empty(ready.Rejections);
    }

    [Fact]
    public void AcceptedMovePutsPartitionIntoCooldown()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(partitionMoveCooldownEvaluations: 2));

        var accepted = state.RecordAcceptedMove(CreateInput());

        Assert.True(accepted.IsAllowed);
        Assert.Equal(2, state.GetPartitionCooldown(0).RemainingEvaluations);

        var immediateRetry = state.EvaluateMove(CreateInput(sourceShardId: 1, targetShardId: 0));

        Assert.False(immediateRetry.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.PartitionInCooldown,
            immediateRetry.Rejections);

        state.AdvanceEvaluation();

        Assert.True(state.GetPartitionCooldown(0).IsActive);

        state.AdvanceEvaluation();

        var cooled = state.EvaluateMove(CreateInput(sourceShardId: 1, targetShardId: 0));

        Assert.True(cooled.IsAllowed);
        Assert.False(state.GetPartitionCooldown(0).IsActive);
    }

    [Fact]
    public void SourceAndTargetShardCooldownsRejectImmediateReuse()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                sourceShardMoveCooldownEvaluations: 2,
                targetShardReceiveCooldownEvaluations: 2));

        state.RecordAcceptedMove(CreateInput(partitionId: 0, sourceShardId: 0, targetShardId: 1));

        var sourceRetry = state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 0, targetShardId: 2));
        var targetRetry = state.EvaluateMove(CreateInput(partitionId: 2, sourceShardId: 2, targetShardId: 1));

        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.SourceShardInCooldown,
            sourceRetry.Rejections);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.TargetShardInCooldown,
            targetRetry.Rejections);

        state.AdvanceEvaluation();
        state.AdvanceEvaluation();

        Assert.True(state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 0, targetShardId: 2)).IsAllowed);
        Assert.True(state.EvaluateMove(CreateInput(partitionId: 2, sourceShardId: 2, targetShardId: 1)).IsAllowed);
    }
}
