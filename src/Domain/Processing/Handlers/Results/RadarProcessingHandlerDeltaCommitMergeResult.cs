namespace RadarPulse.Domain.Processing;

internal sealed class RadarProcessingHandlerDeltaCommitMergeResult
{
    /// <summary>
    /// Creates a handler delta commit merge result with applied values and a diagnostic message.
    /// </summary>
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

    /// <summary>
    /// Gets the merge status produced by commit-time handler delta application.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeStatus Status { get; }

    /// <summary>
    /// Gets the number of handler deltas applied.
    /// </summary>
    public int AppliedDeltaCount { get; }

    /// <summary>
    /// Gets the merged handler values applied during commit.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerDeltaValue> AppliedValues { get; }

    /// <summary>
    /// Gets the diagnostic message associated with a rejected or blocked merge.
    /// </summary>
    public string Message { get; }

    public bool IsRejected =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Rejected;

    public bool IsBlocked =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Blocked;
}
