namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingHandlerDeltaMergeResult
{
    public RadarProcessingHandlerDeltaMergeResult(
        RadarProcessingHandlerDeltaMergeStatus status,
        RadarProcessingHandlerDeltaMergeSummary summary,
        int appliedDeltaCount,
        string message = "")
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentOutOfRangeException.ThrowIfNegative(appliedDeltaCount);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Summary = summary;
        AppliedDeltaCount = appliedDeltaCount;
        Message = message;
    }

    public RadarProcessingHandlerDeltaMergeStatus Status { get; }

    public RadarProcessingHandlerDeltaMergeSummary Summary { get; }

    public int AppliedDeltaCount { get; }

    public string Message { get; }

    public bool IsAccepted =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Accepted;

    public bool IsDuplicate =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Duplicate;

    public bool IsRejected =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Rejected;

    public bool IsBlocked =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Blocked;

    internal static void EnsureKnownStatus(
        RadarProcessingHandlerDeltaMergeStatus status)
    {
        if (status is not RadarProcessingHandlerDeltaMergeStatus.Accepted and
            not RadarProcessingHandlerDeltaMergeStatus.Duplicate and
            not RadarProcessingHandlerDeltaMergeStatus.Rejected and
            not RadarProcessingHandlerDeltaMergeStatus.Blocked)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
