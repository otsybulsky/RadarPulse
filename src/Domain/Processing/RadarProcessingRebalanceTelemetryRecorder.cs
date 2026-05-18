namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceTelemetryRecorder
{
    private readonly RadarProcessingTelemetryRetentionOptions options;
    private readonly Dictionary<RadarProcessingRebalanceSkippedReason, long> skippedReasonCounts = new();
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentDecision> recentDecisions;
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentAcceptedMove> recentAcceptedMoves;
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentValidationFailure> recentValidationFailures;

    private long evaluationCount;
    private long noActionDecisionCount;
    private long acceptedMoveCount;
    private long rejectedCandidateCount;
    private long directHotReliefMoveCount;
    private long coldEvacuationMoveCount;
    private long failedMigrationCount;
    private long validationFailureCount;
    private long quarantineEntryCount;
    private long quarantineClearCount;
    private long quarantineRetryCount;
    private long quarantineReentryCount;

    public RadarProcessingRebalanceTelemetryRecorder(
        RadarProcessingTelemetryRetentionOptions? options = null)
    {
        this.options = options ?? RadarProcessingTelemetryRetentionOptions.Default;

        var retainDetail = this.options.RetentionMode is not RadarProcessingDiagnosticRetentionMode.Counters;
        recentDecisions = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentDecision>(
            retainDetail ? this.options.MaxRetainedDecisions : 0);
        recentAcceptedMoves = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentAcceptedMove>(
            retainDetail ? this.options.MaxRetainedAcceptedMoves : 0);
        recentValidationFailures = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentValidationFailure>(
            retainDetail ? this.options.MaxRetainedValidationFailures : 0);
    }

    public RadarProcessingTelemetryRetentionOptions Options => options;

    public void RecordEvaluation()
    {
        evaluationCount++;
    }

    public void RecordDecision(
        RadarProcessingRebalanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        RecordEvaluation();

        switch (decision.Kind)
        {
            case RadarProcessingRebalanceDecisionKind.NoAction:
                noActionDecisionCount++;
                break;
            case RadarProcessingRebalanceDecisionKind.AcceptedMove:
                acceptedMoveCount++;
                RecordAcceptedMove(decision);
                break;
            case RadarProcessingRebalanceDecisionKind.RejectedCandidate:
                rejectedCandidateCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.Kind, "Unsupported decision kind.");
        }

        RecordSkippedReasons(decision.SkippedReasons);
        recentDecisions.Add(RadarProcessingRebalanceRecentDecision.FromDecision(decision));
    }

    public void RecordValidationResult(
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRebalanceValidationResult validation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);
        ArgumentNullException.ThrowIfNull(validation);

        if (validation.IsValid)
        {
            return;
        }

        validationFailureCount++;

        if (validation.Error == RadarProcessingRebalanceValidationError.MigrationFailed)
        {
            failedMigrationCount++;
        }

        recentValidationFailures.Add(
            RadarProcessingRebalanceRecentValidationFailure.FromResult(
                evaluationSequence,
                topologyVersion,
                validation));
    }

    public void RecordQuarantineEntry() =>
        quarantineEntryCount++;

    public void RecordQuarantineClear() =>
        quarantineClearCount++;

    public void RecordQuarantineRetry() =>
        quarantineRetryCount++;

    public void RecordQuarantineReentry() =>
        quarantineReentryCount++;

    public RadarProcessingRebalanceTelemetrySummary CreateSummary() =>
        new(
            CreateCounters(),
            CreateSkippedReasonCounters(),
            recentDecisions.Snapshot(),
            recentAcceptedMoves.Snapshot(),
            recentValidationFailures.Snapshot(),
            CreateRetentionStats());

    public void Reset()
    {
        evaluationCount = 0;
        noActionDecisionCount = 0;
        acceptedMoveCount = 0;
        rejectedCandidateCount = 0;
        directHotReliefMoveCount = 0;
        coldEvacuationMoveCount = 0;
        failedMigrationCount = 0;
        validationFailureCount = 0;
        quarantineEntryCount = 0;
        quarantineClearCount = 0;
        quarantineRetryCount = 0;
        quarantineReentryCount = 0;
        skippedReasonCounts.Clear();
        recentDecisions.Clear();
        recentAcceptedMoves.Clear();
        recentValidationFailures.Clear();
    }

    private void RecordAcceptedMove(
        RadarProcessingRebalanceDecision decision)
    {
        var move = RadarProcessingRebalanceRecentAcceptedMove.FromDecision(decision);

        switch (move.MoveKind)
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
                throw new ArgumentOutOfRangeException(nameof(decision), move.MoveKind, "Unsupported move kind.");
        }

        recentAcceptedMoves.Add(move);
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
            return Array.AsReadOnly(Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>());
        }

        var counters = skippedReasonCounts
            .OrderBy(static pair => (int)pair.Key)
            .Select(static pair => new RadarProcessingRebalanceSkippedReasonCounter(pair.Key, pair.Value))
            .ToArray();

        return Array.AsReadOnly(counters);
    }

    private RadarProcessingRebalanceRetentionStats CreateRetentionStats() =>
        new(
            recentDecisions.Count,
            recentDecisions.DroppedCount,
            retainedLifecycleTransitionCount: 0,
            droppedLifecycleTransitionCount: 0,
            recentAcceptedMoves.Count,
            recentAcceptedMoves.DroppedCount,
            recentValidationFailures.Count,
            recentValidationFailures.DroppedCount);
}
