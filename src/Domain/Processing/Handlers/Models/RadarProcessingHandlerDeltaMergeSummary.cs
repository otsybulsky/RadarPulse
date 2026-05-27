namespace RadarPulse.Domain.Processing;

/// <summary>
/// Snapshot of ordered handler delta merge progress.
/// </summary>
/// <remarks>
/// The summary exposes the next provider sequence required for ordered commit,
/// pending and applied delta counts, optional merged values, and the first
/// permanent blocker when the merge stream cannot safely continue.
/// </remarks>
public sealed class RadarProcessingHandlerDeltaMergeSummary
{
    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> mergedValues;

    /// <summary>
    /// Creates a merge summary with optional merged value evidence.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeSummary(
        RadarProcessingQueuedBatchSequence nextProviderSequence,
        int pendingDeltaCount,
        int appliedDeltaCount,
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? mergedValues = null,
        RadarProcessingQueuedBatchSequence? firstBlockingSequence = null,
        string firstBlockingReason = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pendingDeltaCount);
        ArgumentOutOfRangeException.ThrowIfNegative(appliedDeltaCount);
        ArgumentNullException.ThrowIfNull(firstBlockingReason);

        NextProviderSequence = nextProviderSequence;
        PendingDeltaCount = pendingDeltaCount;
        AppliedDeltaCount = appliedDeltaCount;
        this.mergedValues = mergedValues is null || mergedValues.Count == 0
            ? Array.Empty<RadarProcessingHandlerDeltaValue>()
            : Array.AsReadOnly(mergedValues.ToArray());
        FirstBlockingSequence = firstBlockingSequence;
        FirstBlockingReason = firstBlockingReason;
    }

    /// <summary>
    /// Next provider sequence required before more deltas can be applied.
    /// </summary>
    public RadarProcessingQueuedBatchSequence NextProviderSequence { get; }

    /// <summary>
    /// Deltas waiting for earlier provider sequences.
    /// </summary>
    public int PendingDeltaCount { get; }

    /// <summary>
    /// Total deltas applied by the coordinator.
    /// </summary>
    public int AppliedDeltaCount { get; }

    /// <summary>
    /// Snapshot of merged values when requested.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerDeltaValue> MergedValues => mergedValues;

    /// <summary>
    /// First provider sequence that permanently blocks merging.
    /// </summary>
    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence { get; }

    /// <summary>
    /// First blocking reason when merging is blocked.
    /// </summary>
    public string FirstBlockingReason { get; }

    /// <summary>
    /// Indicates that the merge stream is permanently blocked.
    /// </summary>
    public bool IsBlocked => FirstBlockingSequence is not null;

    /// <summary>
    /// Indicates that no blockers or pending deltas remain.
    /// </summary>
    public bool IsReady => !IsBlocked && PendingDeltaCount == 0;
}
