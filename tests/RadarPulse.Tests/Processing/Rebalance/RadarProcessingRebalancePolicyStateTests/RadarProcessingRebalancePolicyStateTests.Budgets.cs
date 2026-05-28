using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
    [Fact]
    public void GlobalMoveBudgetCapsAcceptedMovesWithinWindow()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                budgetWindowEvaluationCount: 2,
                globalMoveBudgetPerWindow: 1,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 4));

        var first = state.RecordAcceptedMove(CreateInput(partitionId: 0, sourceShardId: 0, targetShardId: 1));
        var second = state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 2, targetShardId: 0));

        Assert.True(first.IsAllowed);
        Assert.Equal(0, state.GlobalMoveBudget.Remaining);
        Assert.False(second.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted,
            second.Rejections);

        state.AdvanceEvaluation();

        Assert.False(state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 2, targetShardId: 0)).IsAllowed);

        state.AdvanceEvaluation();

        Assert.True(state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 2, targetShardId: 0)).IsAllowed);
        Assert.Equal(1, state.GlobalMoveBudget.Remaining);
    }

    [Fact]
    public void SourceShardMoveBudgetCapsRepeatedMovesWithinWindow()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                budgetWindowEvaluationCount: 3,
                globalMoveBudgetPerWindow: 4,
                sourceShardMoveBudgetPerWindow: 1,
                targetShardReceiveBudgetPerWindow: 4));

        state.RecordAcceptedMove(CreateInput(partitionId: 0, sourceShardId: 0, targetShardId: 1));

        var blocked = state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 0, targetShardId: 2));

        Assert.False(blocked.IsAllowed);
        Assert.Equal(0, state.GetSourceShardMoveBudget(0).Remaining);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted,
            blocked.Rejections);
    }

    [Fact]
    public void TargetShardReceiveBudgetCapsRepeatedReceivesWithinWindow()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                budgetWindowEvaluationCount: 3,
                globalMoveBudgetPerWindow: 4,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 1));

        state.RecordAcceptedMove(CreateInput(partitionId: 0, sourceShardId: 0, targetShardId: 1));

        var blocked = state.EvaluateMove(CreateInput(partitionId: 1, sourceShardId: 2, targetShardId: 1));

        Assert.False(blocked.IsAllowed);
        Assert.Equal(0, state.GetTargetShardReceiveBudget(1).Remaining);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.TargetShardReceiveBudgetExhausted,
            blocked.Rejections);
    }

    [Fact]
    public void PolicyStateAdvancesDeterministicallyAndResetsBudgetsByWindow()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(
                budgetWindowEvaluationCount: 2,
                globalMoveBudgetPerWindow: 1));

        state.RecordAcceptedMove(CreateInput());

        Assert.Equal(0, state.EvaluationSequence);
        Assert.True(state.GlobalMoveBudget.IsExhausted);

        state.AdvanceEvaluation();

        Assert.Equal(1, state.EvaluationSequence);
        Assert.True(state.GlobalMoveBudget.IsExhausted);

        state.AdvanceEvaluation();

        Assert.Equal(2, state.EvaluationSequence);
        Assert.False(state.GlobalMoveBudget.IsExhausted);
    }
}
