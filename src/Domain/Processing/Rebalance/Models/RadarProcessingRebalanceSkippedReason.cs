namespace RadarPulse.Domain.Processing;

/// <summary>
/// Planner and policy reasons that explain why a rebalance move was not published.
/// </summary>
public enum RadarProcessingRebalanceSkippedReason
{
    /// <summary>
    /// No skipped reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// The pressure window does not contain enough sustained pressure to rebalance.
    /// </summary>
    NoSustainedPressure,

    /// <summary>
    /// No hot shard was found in the pressure window.
    /// </summary>
    NoHotShard,

    /// <summary>
    /// No cold shard was available as a target.
    /// </summary>
    NoColdTargetShard,

    /// <summary>
    /// A direct hot partition candidate had no target that remained safe.
    /// </summary>
    DirectHotPartitionHasNoSafeTarget,

    /// <summary>
    /// Candidate projected relief was below the configured threshold.
    /// </summary>
    InsufficientProjectedBenefit,

    /// <summary>
    /// The target shard would become warm after the move.
    /// </summary>
    TargetWouldBecomeWarm,

    /// <summary>
    /// The target shard would become hot or super-hot after the move.
    /// </summary>
    TargetWouldBecomeHot,

    /// <summary>
    /// The target shard would exceed configured headroom.
    /// </summary>
    TargetHeadroomExceeded,

    /// <summary>
    /// The candidate partition is still in cooldown.
    /// </summary>
    CandidatePartitionInCooldown,

    /// <summary>
    /// The candidate partition has not met minimum residency.
    /// </summary>
    CandidatePartitionBelowMinimumResidency,

    /// <summary>
    /// The source shard is still in cooldown.
    /// </summary>
    SourceShardInCooldown,

    /// <summary>
    /// The target shard is still in cooldown.
    /// </summary>
    TargetShardInCooldown,

    /// <summary>
    /// The source shard move-away budget is exhausted.
    /// </summary>
    SourceShardMoveBudgetExhausted,

    /// <summary>
    /// The target shard receive budget is exhausted.
    /// </summary>
    TargetShardReceiveBudgetExhausted,

    /// <summary>
    /// The global move budget is exhausted.
    /// </summary>
    GlobalMoveBudgetExhausted,

    /// <summary>
    /// The partition is classified as intrinsically hot and should not move directly.
    /// </summary>
    PartitionClassifiedIntrinsicHot,

    /// <summary>
    /// The partition is currently quarantined.
    /// </summary>
    PartitionQuarantined,

    /// <summary>
    /// Cold evacuation did not provide enough projected benefit.
    /// </summary>
    ColdEvacuationInsufficientBenefit,

    /// <summary>
    /// A selected move failed migration validation before publication.
    /// </summary>
    MigrationValidationFailed
}
