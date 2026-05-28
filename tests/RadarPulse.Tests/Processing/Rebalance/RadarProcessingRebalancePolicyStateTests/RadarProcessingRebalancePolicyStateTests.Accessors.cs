using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
    [Fact]
    public void AccessorsExposeResidencyCooldownAndBudgets()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                minimumPartitionResidencyEvaluations: 2,
                partitionMoveCooldownEvaluations: 3,
                sourceShardMoveCooldownEvaluations: 3,
                targetShardReceiveCooldownEvaluations: 3,
                globalMoveBudgetPerWindow: 2,
                sourceShardMoveBudgetPerWindow: 2,
                targetShardReceiveBudgetPerWindow: 2));

        state.AdvanceEvaluation();
        state.AdvanceEvaluation();
        state.RecordAcceptedMove(CreateInput(partitionId: 0, sourceShardId: 0, targetShardId: 1));

        var residency = state.GetPartitionResidency(0);
        var partitionCooldown = state.GetPartitionCooldown(0);
        var sourceCooldown = state.GetSourceShardCooldown(0);
        var targetCooldown = state.GetTargetShardCooldown(1);

        Assert.Equal(0, residency.AgeEvaluations);
        Assert.Equal(2, residency.RequiredEvaluations);
        Assert.Equal(3, partitionCooldown.RemainingEvaluations);
        Assert.Equal(3, sourceCooldown.RemainingEvaluations);
        Assert.Equal(3, targetCooldown.RemainingEvaluations);
        Assert.Equal(1, state.GlobalMoveBudget.Remaining);
        Assert.Equal(1, state.GetSourceShardMoveBudget(0).Remaining);
        Assert.Equal(1, state.GetTargetShardReceiveBudget(1).Remaining);
    }
}
