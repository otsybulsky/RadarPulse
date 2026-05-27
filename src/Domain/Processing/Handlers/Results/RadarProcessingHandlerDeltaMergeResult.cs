namespace RadarPulse.Domain.Processing;

/// <summary>
/// Public result of completing one handler delta.
/// </summary>
public sealed class RadarProcessingHandlerDeltaMergeResult
{
    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> appliedValues;

    /// <summary>
    /// Creates a merge result with summary and optional applied values.
    /// </summary>
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

    /// <summary>
    /// Merge outcome.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeStatus Status { get; }

    /// <summary>
    /// Merge coordinator summary after the operation.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeSummary Summary { get; }

    /// <summary>
    /// Number of deltas applied by this completion call.
    /// </summary>
    public int AppliedDeltaCount { get; }

    /// <summary>
    /// Optional diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Values applied by this completion call.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerDeltaValue> AppliedValues => appliedValues;

    /// <summary>
    /// Indicates an accepted merge result.
    /// </summary>
    public bool IsAccepted =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Accepted;

    /// <summary>
    /// Indicates an identical duplicate delta.
    /// </summary>
    public bool IsDuplicate =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Duplicate;

    /// <summary>
    /// Indicates a rejected delta that introduced a blocker.
    /// </summary>
    public bool IsRejected =>
        Status == RadarProcessingHandlerDeltaMergeStatus.Rejected;

    /// <summary>
    /// Indicates a result blocked by a prior permanent blocker.
    /// </summary>
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
