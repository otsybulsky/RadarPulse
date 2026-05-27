namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Deterministic synthetic rebalance workload scenarios.
/// </summary>
public enum RadarProcessingSyntheticRebalanceWorkloadKind
{
    /// <summary>
    /// Balanced source distribution with no expected pressure relief.
    /// </summary>
    Balanced = 0,

    /// <summary>
    /// Sustained hot shard pressure that should produce relief moves.
    /// </summary>
    SustainedHotShard,

    /// <summary>
    /// Hot partition classified as intrinsic before processing.
    /// </summary>
    IntrinsicHotPartition,

    /// <summary>
    /// Alternating pressure spike used to exercise window hysteresis.
    /// </summary>
    OscillatingSpike,

    /// <summary>
    /// Repeated hot pressure constrained by cooldown policy.
    /// </summary>
    CooldownStorm,

    /// <summary>
    /// Quarantine scenario where TTL triggers retry.
    /// </summary>
    QuarantineTtlRetry,

    /// <summary>
    /// Quarantine scenario where sustained cooling clears the partition.
    /// </summary>
    QuarantineSustainedCoolingClear,

    /// <summary>
    /// Quarantine scenario where pressure change triggers retry.
    /// </summary>
    QuarantinePressureChangeRetry,

    /// <summary>
    /// Quarantine scenario where retry re-enters quarantine.
    /// </summary>
    QuarantineRetryReentry,

    /// <summary>
    /// Quarantine scenario where successful relief clears the partition.
    /// </summary>
    QuarantineSuccessfulReliefClear,

    /// <summary>
    /// Longer run with no hot shard.
    /// </summary>
    LongNoHotShard,

    /// <summary>
    /// Longer run dominated by cooldown rejections.
    /// </summary>
    LongCooldownRejection,

    /// <summary>
    /// Longer run dominated by unsafe target rejections.
    /// </summary>
    LongUnsafeTargetRejection,

    /// <summary>
    /// Longer run with mixed skipped rebalance reasons.
    /// </summary>
    LongMixedSkippedReasons,

    /// <summary>
    /// Longer run using counters-only diagnostic retention.
    /// </summary>
    CountersOnlyRetention
}
