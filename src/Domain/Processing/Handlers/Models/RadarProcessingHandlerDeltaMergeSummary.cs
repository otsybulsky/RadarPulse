namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingHandlerDeltaMergeSummary
{
    private readonly IReadOnlyList<RadarProcessingHandlerDeltaValue> mergedValues;

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

    public RadarProcessingQueuedBatchSequence NextProviderSequence { get; }

    public int PendingDeltaCount { get; }

    public int AppliedDeltaCount { get; }

    public IReadOnlyList<RadarProcessingHandlerDeltaValue> MergedValues => mergedValues;

    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence { get; }

    public string FirstBlockingReason { get; }

    public bool IsBlocked => FirstBlockingSequence is not null;

    public bool IsReady => !IsBlocked && PendingDeltaCount == 0;
}
