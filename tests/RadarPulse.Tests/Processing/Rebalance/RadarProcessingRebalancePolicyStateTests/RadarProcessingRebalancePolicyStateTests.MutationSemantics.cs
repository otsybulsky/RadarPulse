using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
    [Fact]
    public void EvaluateMoveDoesNotMutatePolicyState()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                globalMoveBudgetPerWindow: 1,
                sourceShardMoveBudgetPerWindow: 1,
                targetShardReceiveBudgetPerWindow: 1));

        var input = CreateInput();

        Assert.True(state.EvaluateMove(input).IsAllowed);
        Assert.True(state.EvaluateMove(input).IsAllowed);
        Assert.Equal(1, state.GlobalMoveBudget.Remaining);
        Assert.Equal(1, state.GetSourceShardMoveBudget(0).Remaining);
        Assert.Equal(1, state.GetTargetShardReceiveBudget(1).Remaining);
        Assert.False(state.GetPartitionCooldown(0).IsActive);
    }

    [Fact]
    public void RejectedMoveDoesNotConsumeBudgetOrCooldown()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                minimumProjectedBenefit: 1.0,
                partitionMoveCooldownEvaluations: 2));

        var rejected = state.RecordAcceptedMove(CreateInput(projectedBenefit: 0.5));

        Assert.False(rejected.IsAllowed);
        Assert.Equal(1, state.GlobalMoveBudget.Remaining);
        Assert.Equal(1, state.GetSourceShardMoveBudget(0).Remaining);
        Assert.Equal(1, state.GetTargetShardReceiveBudget(1).Remaining);
        Assert.False(state.GetPartitionCooldown(0).IsActive);
    }
}
