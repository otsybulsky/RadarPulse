namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingRebalanceTelemetryRecorder
{
    private void AddRecentDecision(
        RadarProcessingRebalanceDecision decision)
    {
        if (!recentDecisions.CanRetain)
        {
            recentDecisions.Drop();
            return;
        }

        recentDecisions.Add(RadarProcessingRebalanceRecentDecision.FromDecision(decision));
    }

    private void RecordAcceptedMove(
        RadarProcessingRebalanceDecision decision)
    {
        switch (decision.MoveKind)
        {
            case RadarProcessingRebalanceMoveKind.DirectHotRelief:
                directHotReliefMoveCount++;
                break;
            case RadarProcessingRebalanceMoveKind.ColdEvacuation:
                coldEvacuationMoveCount++;
                break;
            case RadarProcessingRebalanceMoveKind.RoomMakingReserved:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.MoveKind, "Unsupported move kind.");
        }

        if (!recentAcceptedMoves.CanRetain)
        {
            recentAcceptedMoves.Drop();
            return;
        }

        recentAcceptedMoves.Add(RadarProcessingRebalanceRecentAcceptedMove.FromDecision(decision));
    }

    private void AddRecentLifecycleTransition(
        RadarProcessingQuarantineTransition transition)
    {
        if (!recentLifecycleTransitions.CanRetain)
        {
            recentLifecycleTransitions.Drop();
            return;
        }

        recentLifecycleTransitions.Add(RadarProcessingRebalanceRecentLifecycleTransition.FromTransition(transition));
    }

    private void RecordSkippedReasons(
        IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons)
    {
        foreach (var reason in skippedReasons)
        {
            RadarProcessingRebalanceSkippedReasonCounter.EnsureExplicitReason(reason);

            skippedReasonCounts.TryGetValue(reason, out var current);
            skippedReasonCounts[reason] = current + 1;
        }
    }

    private RadarProcessingRebalanceTelemetryCounters CreateCounters() =>
        new(
            evaluationCount,
            noActionDecisionCount,
            acceptedMoveCount,
            rejectedCandidateCount,
            directHotReliefMoveCount,
            coldEvacuationMoveCount,
            failedMigrationCount,
            validationFailureCount,
            quarantineEntryCount,
            quarantineClearCount,
            quarantineRetryCount,
            quarantineReentryCount);

    private IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> CreateSkippedReasonCounters()
    {
        if (skippedReasonCounts.Count == 0)
        {
            return Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>();
        }

        var reasons = new RadarProcessingRebalanceSkippedReason[skippedReasonCounts.Count];
        skippedReasonCounts.Keys.CopyTo(reasons, 0);
        Array.Sort(reasons);

        var counters = new RadarProcessingRebalanceSkippedReasonCounter[reasons.Length];
        for (var index = 0; index < reasons.Length; index++)
        {
            var reason = reasons[index];
            counters[index] = new RadarProcessingRebalanceSkippedReasonCounter(
                reason,
                skippedReasonCounts[reason]);
        }

        return Array.AsReadOnly(counters);
    }

    private RadarProcessingRebalanceRetentionStats CreateRetentionStats() =>
        new(
            recentDecisions.Count,
            recentDecisions.DroppedCount,
            recentLifecycleTransitions.Count,
            recentLifecycleTransitions.DroppedCount,
            recentAcceptedMoves.Count,
            recentAcceptedMoves.DroppedCount,
            recentValidationFailures.Count,
            recentValidationFailures.DroppedCount);
}
