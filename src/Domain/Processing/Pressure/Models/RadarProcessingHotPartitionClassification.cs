namespace RadarPulse.Domain.Processing;

/// <summary>
/// Classification assigned to a hot partition by rebalance planning evidence.
/// </summary>
public enum RadarProcessingHotPartitionClassification
{
    /// <summary>
    /// No hot-partition classification is known.
    /// </summary>
    None = 0,

    /// <summary>
    /// The partition appears movable and can be used for direct relief.
    /// </summary>
    MovableHot,

    /// <summary>
    /// The partition appears intrinsically hot and direct movement is blocked.
    /// </summary>
    IntrinsicHot,

    /// <summary>
    /// The partition is quarantined after ineffective relief attempts.
    /// </summary>
    Quarantined
}
