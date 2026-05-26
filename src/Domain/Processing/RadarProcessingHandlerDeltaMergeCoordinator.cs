namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingHandlerDeltaMergeCoordinator
{
    private readonly IRadarProcessingHandlerDeltaMerger merger;
    private readonly SortedDictionary<long, RadarProcessingHandlerDelta> pendingBySequence = new();
    private readonly Dictionary<RadarProcessingHandlerDeltaId, RadarProcessingHandlerDelta> completedById = new();
    private IReadOnlyList<RadarProcessingHandlerDeltaValue> mergedValues =
        Array.Empty<RadarProcessingHandlerDeltaValue>();
    private RadarProcessingQueuedBatchSequence nextProviderSequence;
    private RadarProcessingQueuedBatchSequence? permanentBlockingSequence;
    private string permanentBlockingReason = string.Empty;

    public RadarProcessingHandlerDeltaMergeCoordinator(
        IRadarProcessingHandlerDeltaMerger merger,
        RadarProcessingQueuedBatchSequence? initialProviderSequence = null)
    {
        ArgumentNullException.ThrowIfNull(merger);
        ArgumentException.ThrowIfNullOrWhiteSpace(merger.HandlerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(merger.HandlerContractVersion);

        this.merger = merger;
        nextProviderSequence = initialProviderSequence ?? RadarProcessingQueuedBatchSequence.Initial;
    }

    public RadarProcessingHandlerDeltaMergeSummary CreateSummary() =>
        new(
            nextProviderSequence,
            pendingBySequence.Count,
            completedById.Count,
            mergedValues,
            CreateFirstBlockingSequence(),
            CreateFirstBlockingReason());

    public RadarProcessingHandlerDeltaMergeResult Complete(
        RadarProcessingHandlerDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (permanentBlockingSequence is not null)
        {
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Blocked,
                CreateSummary(),
                appliedDeltaCount: 0,
                permanentBlockingReason);
        }

        var validationError = ValidateDelta(delta);
        if (validationError.Length != 0)
        {
            Block(delta.ProviderSequence, validationError);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(),
                appliedDeltaCount: 0,
                validationError);
        }

        if (completedById.TryGetValue(delta.DeltaId, out var completed))
        {
            if (AreEquivalent(completed, delta))
            {
                return new RadarProcessingHandlerDeltaMergeResult(
                    RadarProcessingHandlerDeltaMergeStatus.Duplicate,
                    CreateSummary(),
                    appliedDeltaCount: 0,
                    "Duplicate handler delta was ignored.");
            }

            var conflict = "Duplicate handler delta id carried different payload.";
            Block(delta.ProviderSequence, conflict);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(),
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
                CreateSummary(),
                appliedDeltaCount: 0,
                message);
        }

        if (pendingBySequence.TryGetValue(sequence, out var pending))
        {
            if (AreEquivalent(pending, delta))
            {
                return new RadarProcessingHandlerDeltaMergeResult(
                    RadarProcessingHandlerDeltaMergeStatus.Duplicate,
                    CreateSummary(),
                    appliedDeltaCount: 0,
                    "Duplicate pending handler delta was ignored.");
            }

            var message = "Conflicting handler deltas completed for the same provider sequence.";
            Block(delta.ProviderSequence, message);
            return new RadarProcessingHandlerDeltaMergeResult(
                RadarProcessingHandlerDeltaMergeStatus.Rejected,
                CreateSummary(),
                appliedDeltaCount: 0,
                message);
        }

        pendingBySequence.Add(sequence, delta);
        var appliedCount = DrainReadyDeltas();
        return new RadarProcessingHandlerDeltaMergeResult(
            RadarProcessingHandlerDeltaMergeStatus.Accepted,
            CreateSummary(),
            appliedCount,
            appliedCount == 0
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

    private int DrainReadyDeltas()
    {
        var appliedCount = 0;
        while (pendingBySequence.Remove(nextProviderSequence.Value, out var delta))
        {
            try
            {
                mergedValues = CopyValues(merger.Merge(mergedValues, delta));
            }
            catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                Block(delta.ProviderSequence, exception.Message);
                break;
            }

            completedById.Add(delta.DeltaId, delta);
            appliedCount++;
            nextProviderSequence = nextProviderSequence.Next();
        }

        return appliedCount;
    }

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
}
