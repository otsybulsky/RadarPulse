namespace RadarPulse.Domain.Processing;

/// <summary>
/// Orders and merges handler deltas by provider sequence.
/// </summary>
/// <remarks>
/// The coordinator accepts deltas out of arrival order but only applies them in
/// contiguous provider-sequence order. Conflicting duplicates, mismatched handler
/// contracts, unsupported schema versions, or merge exceptions create a permanent
/// blocker so downstream commit can fail closed.
/// </remarks>
public sealed partial class RadarProcessingHandlerDeltaMergeCoordinator
{
    private readonly IRadarProcessingHandlerDeltaMerger merger;
    private readonly IRadarProcessingHandlerDeltaAccumulator? accumulator;
    private readonly SortedDictionary<long, RadarProcessingHandlerDelta> pendingBySequence = new();
    private readonly Dictionary<RadarProcessingHandlerDeltaId, RadarProcessingHandlerDelta> completedById = new();
    private IReadOnlyList<RadarProcessingHandlerDeltaValue> mergedValues =
        Array.Empty<RadarProcessingHandlerDeltaValue>();
    private RadarProcessingQueuedBatchSequence nextProviderSequence;
    private RadarProcessingQueuedBatchSequence? permanentBlockingSequence;
    private string permanentBlockingReason = string.Empty;

    /// <summary>
    /// Creates a coordinator for one mergeable handler contract.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeCoordinator(
        IRadarProcessingHandlerDeltaMerger merger,
        RadarProcessingQueuedBatchSequence? initialProviderSequence = null)
    {
        ArgumentNullException.ThrowIfNull(merger);
        ArgumentException.ThrowIfNullOrWhiteSpace(merger.HandlerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(merger.HandlerContractVersion);

        this.merger = merger;
        accumulator = merger is IRadarProcessingHandlerDeltaAccumulatorFactory factory
            ? factory.CreateAccumulator()
            : null;
        nextProviderSequence = initialProviderSequence ?? RadarProcessingQueuedBatchSequence.Initial;
    }

    /// <summary>
    /// Creates a public summary including merged value evidence.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeSummary CreateSummary() =>
        CreateSummary(captureMergedValues: true);

    private RadarProcessingHandlerDeltaMergeSummary CreateSummary(
        bool captureMergedValues) =>
        new(
            nextProviderSequence,
            pendingBySequence.Count,
            completedById.Count,
            captureMergedValues ? CreateMergedValuesSnapshot() : Array.Empty<RadarProcessingHandlerDeltaValue>(),
            CreateFirstBlockingSequence(),
            CreateFirstBlockingReason());

    /// <summary>
    /// Completes the merge and captures merged values for ordered commit.
    /// </summary>
    public RadarProcessingHandlerDeltaCommitMergeResult CompleteForCommit(
        RadarProcessingHandlerDelta delta) =>
        CompleteForCommitCore(delta);
}
