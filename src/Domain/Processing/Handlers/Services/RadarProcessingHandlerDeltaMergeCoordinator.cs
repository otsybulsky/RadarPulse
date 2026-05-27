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
public sealed class RadarProcessingHandlerDeltaMergeCoordinator
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
    /// Completes one handler delta and applies any newly contiguous deltas.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeResult Complete(
        RadarProcessingHandlerDelta delta) =>
        Complete(delta, captureMergedValuesInSummary: true);

    internal RadarProcessingHandlerDeltaCommitMergeResult CompleteForCommit(
        RadarProcessingHandlerDelta delta) =>
        CompleteForCommitCore(delta);

    private RadarProcessingHandlerDeltaMergeResult Complete(
        RadarProcessingHandlerDelta delta,
        bool captureMergedValuesInSummary)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (permanentBlockingSequence is not null)
        {
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Blocked,
                CreateSummary(captureMergedValuesInSummary),
                appliedDeltaCount: 0,
                permanentBlockingReason);
        }

        var validationError = ValidateDelta(delta);
        if (validationError.Length != 0)
        {
            Block(delta.ProviderSequence, validationError);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(captureMergedValuesInSummary),
                appliedDeltaCount: 0,
                validationError);
        }

        if (completedById.TryGetValue(delta.DeltaId, out var completed))
        {
            if (AreEquivalent(completed, delta))
            {
                return new RadarProcessingHandlerDeltaMergeResult(
                    RadarProcessingHandlerDeltaMergeStatus.Duplicate,
                    CreateSummary(captureMergedValuesInSummary),
                    appliedDeltaCount: 0,
                    "Duplicate handler delta was ignored.");
            }

            var conflict = "Duplicate handler delta id carried different payload.";
            Block(delta.ProviderSequence, conflict);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(captureMergedValuesInSummary),
                appliedDeltaCount: 0,
                conflict);
        }

        var sequence = delta.ProviderSequence.Value;
        if (sequence < nextProviderSequence.Value)
        {
            var message = "Handler delta provider sequence has already passed the merge boundary.";
            Block(delta.ProviderSequence, message);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(captureMergedValuesInSummary),
                appliedDeltaCount: 0,
                message);
        }

        if (pendingBySequence.TryGetValue(sequence, out var pending))
        {
            if (AreEquivalent(pending, delta))
            {
                return new RadarProcessingHandlerDeltaMergeResult(
                    RadarProcessingHandlerDeltaMergeStatus.Duplicate,
                    CreateSummary(captureMergedValuesInSummary),
                    appliedDeltaCount: 0,
                    "Duplicate pending handler delta was ignored.");
            }

            var message = "Conflicting handler deltas completed for the same provider sequence.";
            Block(delta.ProviderSequence, message);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(captureMergedValuesInSummary),
                appliedDeltaCount: 0,
                message);
        }

        pendingBySequence.Add(sequence, delta);
        var drain = DrainReadyDeltas();
        return new RadarProcessingHandlerDeltaMergeResult(
            RadarProcessingHandlerDeltaMergeStatus.Accepted,
            CreateSummary(captureMergedValuesInSummary),
            drain.AppliedDeltaCount,
            drain.AppliedDeltaCount == 0
                ? "Handler delta accepted and is waiting for earlier provider sequence."
                : "Handler delta accepted and ready deltas were merged.",
            drain.AppliedValues);
    }

    private RadarProcessingHandlerDeltaCommitMergeResult CompleteForCommitCore(
        RadarProcessingHandlerDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (permanentBlockingSequence is not null)
        {
            return new RadarProcessingHandlerDeltaCommitMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Blocked,
                appliedDeltaCount: 0,
                message: permanentBlockingReason);
        }

        var validationError = ValidateDelta(delta);
        if (validationError.Length != 0)
        {
            Block(delta.ProviderSequence, validationError);
            return new RadarProcessingHandlerDeltaCommitMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                appliedDeltaCount: 0,
                message: validationError);
        }

        if (completedById.TryGetValue(delta.DeltaId, out var completed))
        {
            if (AreEquivalent(completed, delta))
            {
                return new RadarProcessingHandlerDeltaCommitMergeResult(
                    RadarProcessingHandlerDeltaMergeStatus.Duplicate,
                    appliedDeltaCount: 0,
                    message: "Duplicate handler delta was ignored.");
            }

            var conflict = "Duplicate handler delta id carried different payload.";
            Block(delta.ProviderSequence, conflict);
            return new RadarProcessingHandlerDeltaCommitMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                appliedDeltaCount: 0,
                message: conflict);
        }

        var sequence = delta.ProviderSequence.Value;
        if (sequence < nextProviderSequence.Value)
        {
            var message = "Handler delta provider sequence has already passed the merge boundary.";
            Block(delta.ProviderSequence, message);
            return new RadarProcessingHandlerDeltaCommitMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                appliedDeltaCount: 0,
                message: message);
        }

        if (pendingBySequence.TryGetValue(sequence, out var pending))
        {
            if (AreEquivalent(pending, delta))
            {
                return new RadarProcessingHandlerDeltaCommitMergeResult(
                    RadarProcessingHandlerDeltaMergeStatus.Duplicate,
                    appliedDeltaCount: 0,
                    message: "Duplicate pending handler delta was ignored.");
            }

            var message = "Conflicting handler deltas completed for the same provider sequence.";
            Block(delta.ProviderSequence, message);
            return new RadarProcessingHandlerDeltaCommitMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                appliedDeltaCount: 0,
                message: message);
        }

        pendingBySequence.Add(sequence, delta);
        var drain = DrainReadyDeltas();
        return new RadarProcessingHandlerDeltaCommitMergeResult(
            RadarProcessingHandlerDeltaMergeStatus.Accepted,
            drain.AppliedDeltaCount,
            drain.AppliedValues,
            drain.AppliedDeltaCount == 0
                ? "Handler delta accepted and is waiting for earlier provider sequence."
                : "Handler delta accepted and ready deltas were merged.");
    }

    private string ValidateDelta(
        RadarProcessingHandlerDelta delta)
    {
        if (!string.Equals(delta.HandlerName, merger.HandlerName, StringComparison.Ordinal))
        {
            return $"Handler delta name '{delta.HandlerName}' does not match merger '{merger.HandlerName}'.";
        }

        if (!string.Equals(delta.HandlerContractVersion, merger.HandlerContractVersion, StringComparison.Ordinal))
        {
            return "Handler delta contract version does not match the merger contract version.";
        }

        if (delta.SchemaVersion != RadarProcessingHandlerDelta.CurrentSchemaVersion)
        {
            return "Handler delta schema version is not supported by the merge coordinator.";
        }

        return string.Empty;
    }

    private DrainReadyDeltasResult DrainReadyDeltas()
    {
        var appliedCount = 0;
        IReadOnlyList<RadarProcessingHandlerDeltaValue> appliedValues =
            Array.Empty<RadarProcessingHandlerDeltaValue>();
        List<RadarProcessingHandlerDeltaValue>? combinedAppliedValues = null;
        while (pendingBySequence.Remove(nextProviderSequence.Value, out var delta))
        {
            if (appliedCount > 0 &&
                appliedValues.Count != 0 &&
                combinedAppliedValues is null)
            {
                combinedAppliedValues = new List<RadarProcessingHandlerDeltaValue>(appliedValues.Count);
                combinedAppliedValues.AddRange(appliedValues);
                appliedValues = Array.Empty<RadarProcessingHandlerDeltaValue>();
            }

            IReadOnlyList<RadarProcessingHandlerDeltaValue> deltaAppliedValues;
            try
            {
                deltaAppliedValues = MergeDelta(delta);
            }
            catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                Block(delta.ProviderSequence, exception.Message);
                break;
            }

            if (deltaAppliedValues.Count != 0)
            {
                if (combinedAppliedValues is null)
                {
                    appliedValues = deltaAppliedValues;
                }
                else
                {
                    combinedAppliedValues.AddRange(deltaAppliedValues);
                }
            }

            completedById.Add(delta.DeltaId, delta);
            appliedCount++;
            nextProviderSequence = nextProviderSequence.Next();
        }

        return new DrainReadyDeltasResult(
            appliedCount,
            combinedAppliedValues ?? appliedValues);
    }

    private IReadOnlyList<RadarProcessingHandlerDeltaValue> MergeDelta(
        RadarProcessingHandlerDelta delta)
    {
        if (accumulator is not null)
        {
            return accumulator.Merge(delta);
        }

        mergedValues = CopyValues(merger.Merge(mergedValues, delta));
        return mergedValues;
    }

    private IReadOnlyList<RadarProcessingHandlerDeltaValue> CreateMergedValuesSnapshot() =>
        accumulator is null
            ? mergedValues
            : CopyValues(accumulator.CreateMergedValuesSnapshot());

    private RadarProcessingQueuedBatchSequence? CreateFirstBlockingSequence()
    {
        if (permanentBlockingSequence is not null)
        {
            return permanentBlockingSequence;
        }

        if (pendingBySequence.Count == 0)
        {
            return null;
        }

        return nextProviderSequence;
    }

    private string CreateFirstBlockingReason()
    {
        if (permanentBlockingReason.Length != 0)
        {
            return permanentBlockingReason;
        }

        return pendingBySequence.Count == 0
            ? string.Empty
            : $"Waiting for handler delta at provider sequence {nextProviderSequence.Value}.";
    }

    private void Block(
        RadarProcessingQueuedBatchSequence sequence,
        string reason)
    {
        permanentBlockingSequence = sequence;
        permanentBlockingReason = string.IsNullOrWhiteSpace(reason)
            ? "Handler delta merge is blocked."
            : reason;
    }

    private static IReadOnlyList<RadarProcessingHandlerDeltaValue> CopyValues(
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values) =>
        values is null || values.Count == 0
            ? Array.Empty<RadarProcessingHandlerDeltaValue>()
            : Array.AsReadOnly(values.ToArray());

    private static bool AreEquivalent(
        RadarProcessingHandlerDelta first,
        RadarProcessingHandlerDelta second) =>
        string.Equals(first.HandlerName, second.HandlerName, StringComparison.Ordinal) &&
        string.Equals(first.HandlerContractVersion, second.HandlerContractVersion, StringComparison.Ordinal) &&
        first.ProviderSequence == second.ProviderSequence &&
        first.DurableBatchId == second.DurableBatchId &&
        first.EventCount == second.EventCount &&
        first.SourceCount == second.SourceCount &&
        first.PayloadValueCount == second.PayloadValueCount &&
        first.InputChecksum == second.InputChecksum &&
        first.DeltaId == second.DeltaId &&
        first.SchemaVersion == second.SchemaVersion &&
        first.Values.SequenceEqual(second.Values);

    private readonly record struct DrainReadyDeltasResult(
        int AppliedDeltaCount,
        IReadOnlyList<RadarProcessingHandlerDeltaValue> AppliedValues);
}
