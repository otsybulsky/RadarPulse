using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalancePolicyStateTests
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
    public void ProjectedBenefitBelowThresholdIsRejected()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(minimumProjectedBenefit: 0.2));

        var result = state.EvaluateMove(CreateInput(projectedBenefit: 0.1));

        Assert.False(result.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            result.Rejections);
    }

    [Fact]
    public void TargetHeadroomGateRejectsProjectedPressure()
    {
        var state = new RadarProcessingRebalancePolicyState(
            partitionCount: 4,
            shardCount: 3,
            CreateOptions(targetHeadroomThreshold: 10.0));

        var result = state.EvaluateMove(CreateInput(targetProjectedPressure: 11.0));

        Assert.False(result.IsAllowed);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded,
            result.Rejections);
    }

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

    [Fact]
    public void OptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(budgetWindowEvaluationCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(globalMoveBudgetPerWindow: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(minimumPartitionResidencyEvaluations: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(partitionMoveCooldownEvaluations: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(minimumProjectedBenefit: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRebalanceOptions(targetHeadroomThreshold: double.PositiveInfinity));
    }

    [Fact]
    public void MovePolicyInputRejectsInvalidShape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(partitionId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(sourceShardId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(targetShardId: -1));
        Assert.Throws<ArgumentException>(() =>
            CreateInput(sourceShardId: 1, targetShardId: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInput(projectedBenefit: double.NaN));
    }

    private static RadarProcessingRebalanceOptions CreateOptions(
        int budgetWindowEvaluationCount = 1,
        int globalMoveBudgetPerWindow = 1,
        int sourceShardMoveBudgetPerWindow = 1,
        int targetShardReceiveBudgetPerWindow = 1,
        int minimumPartitionResidencyEvaluations = 0,
        int partitionMoveCooldownEvaluations = 0,
        int sourceShardMoveCooldownEvaluations = 0,
        int targetShardReceiveCooldownEvaluations = 0,
        double minimumProjectedBenefit = 0.0,
        double targetHeadroomThreshold = double.MaxValue) =>
        new(
            budgetWindowEvaluationCount,
            globalMoveBudgetPerWindow,
            sourceShardMoveBudgetPerWindow,
            targetShardReceiveBudgetPerWindow,
            minimumPartitionResidencyEvaluations,
            partitionMoveCooldownEvaluations,
            sourceShardMoveCooldownEvaluations,
            targetShardReceiveCooldownEvaluations,
            minimumProjectedBenefit,
            targetHeadroomThreshold);

    private static RadarProcessingRebalanceMovePolicyInput CreateInput(
        int partitionId = 0,
        int sourceShardId = 0,
        int targetShardId = 1,
        double projectedBenefit = 1.0,
        double targetProjectedPressure = 0.0) =>
        new(
            partitionId,
            sourceShardId,
            targetShardId,
            projectedBenefit,
            new RadarProcessingPressureScore(targetProjectedPressure));
}
