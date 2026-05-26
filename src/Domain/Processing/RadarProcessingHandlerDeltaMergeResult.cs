namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingHandlerDeltaMergeResult
{
    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> appliedValues;

    public RadarProcessingHandlerDeltaMergeResult(
        RadarProcessingHandlerDeltaMergeStatus status,
        RadarProcessingHandlerDeltaMergeSummary summary,
        int appliedDeltaCount,
        string message = "",
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? appliedValues = null)
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentOutOfRangeException.ThrowIfNegative(appliedDeltaCount);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Summary = summary;
        AppliedDeltaCount = appliedDeltaCount;
        Message = message;
        this.appliedValues = appliedValues is null || appliedValues.Count == 0
            ? Array.Empty<RadarProcessingHandlerDeltaValue>()
            : Array.AsReadOnly(appliedValues.ToArray());
    }

    public RadarProcessingHandlerDeltaMergeStatus Status { get; }

    public RadarProcessingHandlerDeltaMergeSummary Summary { get; }

    public int AppliedDeltaCount { get; }

    public string Message { get; }

    public IReadOnlyList<RadarProcessingHandlerDeltaValue> AppliedValues => appliedValues;

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
