namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRebalancePolicyRejection
{
    None = 0,
    PartitionBelowMinimumResidency,
    PartitionInCooldown,
    SourceShardInCooldown,
    TargetShardInCooldown,
    GlobalMoveBudgetExhausted,
    SourceShardMoveBudgetExhausted,
    TargetShardReceiveBudgetExhausted,
    InsufficientProjectedBenefit,
    TargetHeadroomExceeded
}
