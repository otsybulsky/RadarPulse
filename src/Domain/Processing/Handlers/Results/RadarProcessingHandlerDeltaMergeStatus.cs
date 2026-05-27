namespace RadarPulse.Domain.Processing;

/// <summary>
/// Outcome of completing one handler delta against an ordered merge coordinator.
/// </summary>
public enum RadarProcessingHandlerDeltaMergeStatus
{
    /// <summary>
    /// Delta was accepted and any ready ordered deltas were applied.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// Delta was an identical duplicate and was ignored.
    /// </summary>
    Duplicate = 2,

    /// <summary>
    /// Delta was rejected and the stream is blocked by invalid evidence.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// Delta could not be accepted because a prior permanent blocker exists.
    /// </summary>
    Blocked = 4
}
