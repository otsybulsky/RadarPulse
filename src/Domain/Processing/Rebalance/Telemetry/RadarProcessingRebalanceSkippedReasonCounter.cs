namespace RadarPulse.Domain.Processing;

/// <summary>
/// Counter for a specific rebalance skipped reason.
/// </summary>
public sealed record RadarProcessingRebalanceSkippedReasonCounter
{
    /// <summary>
    /// Creates a skipped-reason counter.
    /// </summary>
    public RadarProcessingRebalanceSkippedReasonCounter(
        RadarProcessingRebalanceSkippedReason reason,
        long count)
    {
        EnsureExplicitReason(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        Reason = reason;
        Count = count;
    }

    /// <summary>
    /// Skipped reason being counted.
    /// </summary>
    public RadarProcessingRebalanceSkippedReason Reason { get; }

    /// <summary>
    /// Number of times the reason was recorded.
    /// </summary>
    public long Count { get; }

    internal static void EnsureExplicitReason(
        RadarProcessingRebalanceSkippedReason reason)
    {
        if (!Enum.IsDefined(reason) || reason == RadarProcessingRebalanceSkippedReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Skipped reason must be explicit.");
        }
    }
}
