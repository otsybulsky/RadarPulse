namespace RadarPulse.Domain.Processing;

/// <summary>
/// Tracks how much of a rebalance move budget has been consumed.
/// </summary>
public readonly record struct RadarProcessingRebalanceBudget
{
    /// <summary>
    /// Creates a budget snapshot.
    /// </summary>
    public RadarProcessingRebalanceBudget(
        int limit,
        int used)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        ArgumentOutOfRangeException.ThrowIfNegative(used);

        Limit = limit;
        Used = used;
    }

    /// <summary>
    /// Maximum moves allowed in the budget window.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Moves already consumed in the budget window.
    /// </summary>
    public int Used { get; }

    /// <summary>
    /// Remaining moves, never below zero.
    /// </summary>
    public int Remaining => Math.Max(0, Limit - Used);

    /// <summary>
    /// Indicates whether the budget rejects additional moves.
    /// </summary>
    public bool IsExhausted => Used >= Limit;
}
