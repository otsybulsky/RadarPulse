using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalancePolicyStateTests
{
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
