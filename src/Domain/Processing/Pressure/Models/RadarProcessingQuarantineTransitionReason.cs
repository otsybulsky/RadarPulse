namespace RadarPulse.Domain.Processing;

/// <summary>
/// Explicit reason for a quarantine lifecycle transition.
/// </summary>
public enum RadarProcessingQuarantineTransitionReason
{
    /// <summary>
    /// No transition reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// A partition entered quarantine.
    /// </summary>
    EnteredQuarantine = 1,

    /// <summary>
    /// Time-to-live elapsed and the partition became retry-eligible.
    /// </summary>
    MarkedRetryEligibleByTtl = 2,

    /// <summary>
    /// Sustained cooling made the partition retry-eligible.
    /// </summary>
    MarkedRetryEligibleBySustainedCooling = 3,

    /// <summary>
    /// Material pressure change made the partition retry-eligible.
    /// </summary>
    MarkedRetryEligibleByPressureChange = 4,

    /// <summary>
    /// Sustained cooling cleared quarantine.
    /// </summary>
    ClearedBySustainedCooling = 5,

    /// <summary>
    /// A retry showed effective relief and cleared quarantine.
    /// </summary>
    ClearedByEffectiveRelief = 6,

    /// <summary>
    /// Quarantine was cleared by explicit caller action.
    /// </summary>
    ClearedExplicitly = 7,

    /// <summary>
    /// A retry-eligible partition reentered quarantine.
    /// </summary>
    ReenteredQuarantine = 8
}
