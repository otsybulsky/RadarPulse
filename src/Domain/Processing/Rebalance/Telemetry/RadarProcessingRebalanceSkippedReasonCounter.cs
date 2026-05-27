namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceSkippedReasonCounter
{
    public RadarProcessingRebalanceSkippedReasonCounter(
        RadarProcessingRebalanceSkippedReason reason,
        long count)
    {
        EnsureExplicitReason(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        Reason = reason;
        Count = count;
    }

    public RadarProcessingRebalanceSkippedReason Reason { get; }

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
