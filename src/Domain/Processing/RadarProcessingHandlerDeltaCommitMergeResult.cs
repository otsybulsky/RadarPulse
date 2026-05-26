namespace RadarPulse.Domain.Processing;

internal sealed class RadarProcessingHandlerDeltaCommitMergeResult
{
    public RadarProcessingHandlerDeltaCommitMergeResult(
        RadarProcessingHandlerDeltaMergeStatus status,
        int appliedDeltaCount,
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? appliedValues = null,
        string message = "")
    {
        RadarProcessingHandlerDeltaMergeResult.EnsureKnownStatus(status);
        ArgumentOutOfRangeException.ThrowIfNegative(appliedDeltaCount);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        AppliedDeltaCount = appliedDeltaCount;
        AppliedValues = appliedValues is null || appliedValues.Count == 0
            ? Array.Empty<RadarProcessingHandlerDeltaValue>()
            : appliedValues;
        Message = message;
    }

    public RadarProcessingHandlerDeltaMergeStatus Status { get; }

    public int AppliedDeltaCount { get; }

    public IReadOnlyList<RadarProcessingHandlerDeltaValue> AppliedValues { get; }

    public string Message { get; }

    public bool IsRejected =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Rejected;

    public bool IsBlocked =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Blocked;
}
