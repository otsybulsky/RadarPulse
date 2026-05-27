namespace RadarPulse.Domain.Processing;

/// <summary>
/// Strategy that produced a rebalance move candidate.
/// </summary>
public enum RadarProcessingRebalanceMoveKind
{
    /// <summary>
    /// No move strategy.
    /// </summary>
    None = 0,

    /// <summary>
    /// Move a hot partition directly away from a hot source shard.
    /// </summary>
    DirectHotRelief,

    /// <summary>
    /// Move a colder partition away from a hot source shard to create relief.
    /// </summary>
    ColdEvacuation,

    /// <summary>
    /// Reserved for future room-making move planning without current runtime use.
    /// </summary>
    RoomMakingReserved
}
