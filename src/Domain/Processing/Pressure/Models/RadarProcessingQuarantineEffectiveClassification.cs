namespace RadarPulse.Domain.Processing;

/// <summary>
/// Effective lifecycle classification for a partition after quarantine policy is applied.
/// </summary>
public enum RadarProcessingQuarantineEffectiveClassification
{
    /// <summary>
    /// No effective classification is active.
    /// </summary>
    None = 0,

    /// <summary>
    /// The partition is hot but remains eligible for direct movement.
    /// </summary>
    MovableHot = 1,

    /// <summary>
    /// The partition is intrinsically hot and blocks direct movement.
    /// </summary>
    IntrinsicHot = 2,

    /// <summary>
    /// The partition is quarantined and blocks direct movement.
    /// </summary>
    Quarantined = 3,

    /// <summary>
    /// The partition may be retried after quarantine evidence changed.
    /// </summary>
    RetryEligible = 4
}
