namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRebalanceSkippedReason
{
    None = 0,
    NoSustainedPressure,
    NoHotShard,
    NoColdTargetShard,
    DirectHotPartitionHasNoSafeTarget,
    InsufficientProjectedBenefit,
    TargetWouldBecomeWarm,
    TargetWouldBecomeHot,
    TargetHeadroomExceeded,
    CandidatePartitionInCooldown,
    CandidatePartitionBelowMinimumResidency,
    SourceShardInCooldown,
    TargetShardInCooldown,
    SourceShardMoveBudgetExhausted,
    TargetShardReceiveBudgetExhausted,
    GlobalMoveBudgetExhausted,
    PartitionClassifiedIntrinsicHot,
    PartitionQuarantined,
    ColdEvacuationInsufficientBenefit,
    MigrationValidationFailed
}
