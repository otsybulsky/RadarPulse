namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reasons a candidate move can be rejected by rebalance policy.
/// </summary>
public enum RadarProcessingRebalancePolicyRejection
{
    /// <summary>
    /// No rejection.
    /// </summary>
    None = 0,

    /// <summary>
    /// The partition has not remained on its owner long enough.
    /// </summary>
    PartitionBelowMinimumResidency,

    /// <summary>
    /// The partition is still cooling down after a previous move.
    /// </summary>
    PartitionInCooldown,

    /// <summary>
    /// The source shard recently moved a partition away.
    /// </summary>
    SourceShardInCooldown,

    /// <summary>
    /// The target shard recently received a partition.
    /// </summary>
    TargetShardInCooldown,

    /// <summary>
    /// The global move budget is exhausted.
    /// </summary>
    GlobalMoveBudgetExhausted,

    /// <summary>
    /// The source shard move-away budget is exhausted.
    /// </summary>
    SourceShardMoveBudgetExhausted,

    /// <summary>
    /// The target shard receive budget is exhausted.
    /// </summary>
    TargetShardReceiveBudgetExhausted,

    /// <summary>
    /// The projected pressure relief is below the configured threshold.
    /// </summary>
    InsufficientProjectedBenefit,

    /// <summary>
    /// The target shard would exceed the configured headroom threshold.
    /// </summary>
    TargetHeadroomExceeded
}
