namespace RadarPulse.Domain.Processing;

/// <summary>
/// Policy thresholds that constrain partition rebalance moves.
/// </summary>
/// <remarks>
/// These options make rebalance conservative by limiting move frequency,
/// requiring partition residency, enforcing shard cooldowns, and rejecting
/// candidates that do not create enough projected pressure relief.
/// </remarks>
public sealed record RadarProcessingRebalanceOptions
{
    /// <summary>
    /// Default conservative rebalance policy.
    /// </summary>
    public static RadarProcessingRebalanceOptions Default { get; } = new();

    /// <summary>
    /// Creates rebalance policy options.
    /// </summary>
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

    /// <summary>
    /// Number of evaluations that share the same move budgets.
    /// </summary>
    public int BudgetWindowEvaluationCount { get; }

    /// <summary>
    /// Maximum accepted moves across all shards per budget window.
    /// </summary>
    public int GlobalMoveBudgetPerWindow { get; }

    /// <summary>
    /// Maximum moves away from one source shard per budget window.
    /// </summary>
    public int SourceShardMoveBudgetPerWindow { get; }

    /// <summary>
    /// Maximum moves into one target shard per budget window.
    /// </summary>
    public int TargetShardReceiveBudgetPerWindow { get; }

    /// <summary>
    /// Evaluations a partition must remain resident before another move is allowed.
    /// </summary>
    public int MinimumPartitionResidencyEvaluations { get; }

    /// <summary>
    /// Evaluations a moved partition stays in cooldown.
    /// </summary>
    public int PartitionMoveCooldownEvaluations { get; }

    /// <summary>
    /// Evaluations a source shard stays in cooldown after moving a partition away.
    /// </summary>
    public int SourceShardMoveCooldownEvaluations { get; }

    /// <summary>
    /// Evaluations a target shard stays in cooldown after receiving a partition.
    /// </summary>
    public int TargetShardReceiveCooldownEvaluations { get; }

    /// <summary>
    /// Minimum projected pressure relief required for a candidate.
    /// </summary>
    public double MinimumProjectedBenefit { get; }

    /// <summary>
    /// Maximum allowed projected pressure for the target shard.
    /// </summary>
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
