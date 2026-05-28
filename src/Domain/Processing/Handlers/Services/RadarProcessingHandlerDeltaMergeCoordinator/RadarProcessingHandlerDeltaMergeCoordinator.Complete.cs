namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingHandlerDeltaMergeCoordinator
{
    /// <summary>
    /// Completes one handler delta and applies any newly contiguous deltas.
    /// </summary>
    public RadarProcessingHandlerDeltaMergeResult Complete(
        RadarProcessingHandlerDelta delta) =>
        Complete(delta, captureMergedValuesInSummary: true);

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
}
