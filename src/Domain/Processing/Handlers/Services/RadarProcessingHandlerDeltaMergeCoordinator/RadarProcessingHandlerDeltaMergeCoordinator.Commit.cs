namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingHandlerDeltaMergeCoordinator
{
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
}
