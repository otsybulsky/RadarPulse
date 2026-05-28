namespace RadarPulse.Domain.Processing;

/// <summary>
/// Mutable recorder for rebalance decisions, moves, validation failures, and lifecycle transitions.
/// </summary>
/// <remarks>
/// The recorder always maintains aggregate counters. Retained detail windows are
/// bounded by <see cref="RadarProcessingTelemetryRetentionOptions"/> and count
/// dropped entries so diagnostics can tell when detail was trimmed.
/// </remarks>
public sealed partial class RadarProcessingRebalanceTelemetryRecorder
{
    private readonly RadarProcessingTelemetryRetentionOptions options;
    private readonly Dictionary<RadarProcessingRebalanceSkippedReason, long> skippedReasonCounts = new();
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentDecision> recentDecisions;
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentLifecycleTransition> recentLifecycleTransitions;
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

    /// <summary>
    /// Creates a rebalance telemetry recorder.
    /// </summary>
    public RadarProcessingRebalanceTelemetryRecorder(
        RadarProcessingTelemetryRetentionOptions? options = null)
    {
        this.options = options ?? RadarProcessingTelemetryRetentionOptions.Default;

        var retainDetail = this.options.RetentionMode is not RadarProcessingDiagnosticRetentionMode.Counters;
        recentDecisions = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentDecision>(
            retainDetail ? this.options.MaxRetainedDecisions : 0);
        recentLifecycleTransitions = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentLifecycleTransition>(
            retainDetail ? this.options.MaxRetainedLifecycleTransitions : 0);
        recentAcceptedMoves = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentAcceptedMove>(
            retainDetail ? this.options.MaxRetainedAcceptedMoves : 0);
        recentValidationFailures = new RadarProcessingBoundedTelemetryWindow<RadarProcessingRebalanceRecentValidationFailure>(
            retainDetail ? this.options.MaxRetainedValidationFailures : 0);
    }

    /// <summary>
    /// Retention options used by the recorder.
    /// </summary>
    public RadarProcessingTelemetryRetentionOptions Options => options;

    /// <summary>
    /// Records an evaluation without attaching decision detail.
    /// </summary>
    public void RecordEvaluation()
    {
        evaluationCount++;
    }

    /// <summary>
    /// Records a planner decision and any skipped reasons or accepted move detail.
    /// </summary>
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
        AddRecentDecision(decision);
    }

    /// <summary>
    /// Records an invalid rebalance validation result.
    /// </summary>
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

        if (!recentValidationFailures.CanRetain)
        {
            recentValidationFailures.Drop();
            return;
        }

        recentValidationFailures.Add(
            RadarProcessingRebalanceRecentValidationFailure.FromResult(
                evaluationSequence,
                topologyVersion,
                validation));
    }

    /// <summary>
    /// Increments the quarantine entry counter.
    /// </summary>
    public void RecordQuarantineEntry() =>
        quarantineEntryCount++;

    /// <summary>
    /// Increments the quarantine clear counter.
    /// </summary>
    public void RecordQuarantineClear() =>
        quarantineClearCount++;

    /// <summary>
    /// Increments the quarantine retry counter.
    /// </summary>
    public void RecordQuarantineRetry() =>
        quarantineRetryCount++;

    /// <summary>
    /// Increments the quarantine reentry counter.
    /// </summary>
    public void RecordQuarantineReentry() =>
        quarantineReentryCount++;

    /// <summary>
    /// Records a quarantine lifecycle transition and retains bounded detail.
    /// </summary>
    public void RecordQuarantineTransition(
        RadarProcessingQuarantineTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        switch (transition.Reason)
        {
            case RadarProcessingQuarantineTransitionReason.EnteredQuarantine:
                quarantineEntryCount++;
                break;
            case RadarProcessingQuarantineTransitionReason.ClearedBySustainedCooling:
            case RadarProcessingQuarantineTransitionReason.ClearedByEffectiveRelief:
            case RadarProcessingQuarantineTransitionReason.ClearedExplicitly:
                quarantineClearCount++;
                break;
            case RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByTtl:
            case RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleBySustainedCooling:
            case RadarProcessingQuarantineTransitionReason.MarkedRetryEligibleByPressureChange:
                quarantineRetryCount++;
                break;
            case RadarProcessingQuarantineTransitionReason.ReenteredQuarantine:
                quarantineReentryCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition), transition.Reason, "Unsupported quarantine transition reason.");
        }

        AddRecentLifecycleTransition(transition);
    }

    /// <summary>
    /// Creates an immutable telemetry summary from current counters and retained detail.
    /// </summary>
    public RadarProcessingRebalanceTelemetrySummary CreateSummary() =>
        new(
            CreateCounters(),
            CreateSkippedReasonCounters(),
            recentDecisions.Snapshot(),
            recentAcceptedMoves.Snapshot(),
            recentValidationFailures.Snapshot(),
            CreateRetentionStats(),
            recentLifecycleTransitions.Snapshot());

    /// <summary>
    /// Clears counters, skipped-reason counts, retained detail, and drop counts.
    /// </summary>
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
        recentLifecycleTransitions.Clear();
        recentAcceptedMoves.Clear();
        recentValidationFailures.Clear();
    }

}
