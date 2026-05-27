namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceOptions
{
    public static RadarProcessingRebalanceOptions Default { get; } = new();

    public RadarProcessingRebalanceOptions(
        int budgetWindowEvaluationCount = 1,
        int globalMoveBudgetPerWindow = 1,
        int sourceShardMoveBudgetPerWindow = 1,
        int targetShardReceiveBudgetPerWindow = 1,
        int minimumPartitionResidencyEvaluations = 3,
        int partitionMoveCooldownEvaluations = 5,
        int sourceShardMoveCooldownEvaluations = 1,
        int targetShardReceiveCooldownEvaluations = 1,
        double minimumProjectedBenefit = 0.05,
        double targetHeadroomThreshold = double.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetWindowEvaluationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(globalMoveBudgetPerWindow);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceShardMoveBudgetPerWindow);
        ArgumentOutOfRangeException.ThrowIfNegative(targetShardReceiveBudgetPerWindow);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumPartitionResidencyEvaluations);
        ArgumentOutOfRangeException.ThrowIfNegative(partitionMoveCooldownEvaluations);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceShardMoveCooldownEvaluations);
        ArgumentOutOfRangeException.ThrowIfNegative(targetShardReceiveCooldownEvaluations);
        ThrowIfInvalidDouble(minimumProjectedBenefit, nameof(minimumProjectedBenefit));
        ThrowIfInvalidDouble(targetHeadroomThreshold, nameof(targetHeadroomThreshold));

        BudgetWindowEvaluationCount = budgetWindowEvaluationCount;
        GlobalMoveBudgetPerWindow = globalMoveBudgetPerWindow;
        SourceShardMoveBudgetPerWindow = sourceShardMoveBudgetPerWindow;
        TargetShardReceiveBudgetPerWindow = targetShardReceiveBudgetPerWindow;
        MinimumPartitionResidencyEvaluations = minimumPartitionResidencyEvaluations;
        PartitionMoveCooldownEvaluations = partitionMoveCooldownEvaluations;
        SourceShardMoveCooldownEvaluations = sourceShardMoveCooldownEvaluations;
        TargetShardReceiveCooldownEvaluations = targetShardReceiveCooldownEvaluations;
        MinimumProjectedBenefit = minimumProjectedBenefit;
        TargetHeadroomThreshold = targetHeadroomThreshold;
    }

    public int BudgetWindowEvaluationCount { get; }

    public int GlobalMoveBudgetPerWindow { get; }

    public int SourceShardMoveBudgetPerWindow { get; }

    public int TargetShardReceiveBudgetPerWindow { get; }

    public int MinimumPartitionResidencyEvaluations { get; }

    public int PartitionMoveCooldownEvaluations { get; }

    public int SourceShardMoveCooldownEvaluations { get; }

    public int TargetShardReceiveCooldownEvaluations { get; }

    public double MinimumProjectedBenefit { get; }

    public double TargetHeadroomThreshold { get; }

    private static void ThrowIfInvalidDouble(
        double value,
        string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be finite and non-negative.");
        }
    }
}
