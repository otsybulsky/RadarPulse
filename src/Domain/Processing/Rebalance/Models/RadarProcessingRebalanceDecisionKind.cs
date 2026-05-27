namespace RadarPulse.Domain.Processing;

/// <summary>
/// Outcome category produced by a rebalance planner.
/// </summary>
public enum RadarProcessingRebalanceDecisionKind
{
    /// <summary>
    /// The planner did not find a candidate to attempt.
    /// </summary>
    NoAction = 0,

    /// <summary>
    /// A candidate satisfied planning and policy checks.
    /// </summary>
    AcceptedMove,

    /// <summary>
    /// A candidate was found but rejected with reasons.
    /// </summary>
    RejectedCandidate
}
