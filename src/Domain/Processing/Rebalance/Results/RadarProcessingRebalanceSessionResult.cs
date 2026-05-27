namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of a processing pass plus optional rebalance evaluation.
/// </summary>
/// <remarks>
/// The result keeps the raw processing result beside derived pressure, planner
/// decisions, migration publication, handoff validation, quarantine transitions,
/// retained telemetry, and validation posture for inspection by product surfaces.
/// </remarks>
public sealed class RadarProcessingRebalanceSessionResult
{
    /// <summary>
    /// Creates a rebalance session result.
    /// </summary>
    public RadarProcessingRebalanceSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingPressureSample? pressureSample,
        RadarProcessingRebalanceDecision? directHotReliefDecision,
        RadarProcessingRebalanceDecision? coldEvacuationDecision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation,
        RadarProcessingTopology? currentTopology = null,
        IReadOnlyCollection<RadarProcessingQuarantineTransition>? quarantineTransitions = null,
        RadarProcessingRebalanceTelemetrySummary? telemetrySummary = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic,
        RadarProcessingRebalanceValidationResult? validation = null)
    {
        ArgumentNullException.ThrowIfNull(processingResult);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (migrationResult is not null &&
            (directHotReliefDecision?.HasAcceptedMove != true &&
             coldEvacuationDecision?.HasAcceptedMove != true))
        {
            throw new ArgumentException(
                "Migration results require an accepted rebalance decision.",
                nameof(migrationResult));
        }

        ProcessingResult = processingResult;
        PressureSample = pressureSample;
        DirectHotReliefDecision = directHotReliefDecision;
        ColdEvacuationDecision = coldEvacuationDecision;
        MigrationResult = migrationResult;
        HandoffValidation = handoffValidation;
        QuarantineTransitions = CopyRequired(quarantineTransitions ?? Array.Empty<RadarProcessingQuarantineTransition>());
        TelemetrySummary = telemetrySummary ?? RadarProcessingRebalanceTelemetrySummary.Empty;
        ValidationProfile = validationProfile;
        Validation = validation ?? (currentTopology is null
            ? RadarProcessingRebalanceValidationResult.Valid()
            : RadarProcessingRebalanceValidator.ValidateSessionResult(this, currentTopology, validationProfile));
    }

    /// <summary>
    /// Underlying processing result.
    /// </summary>
    public RadarProcessingResult ProcessingResult { get; }

    /// <summary>
    /// Worker telemetry from async processing, when present.
    /// </summary>
    public RadarProcessingWorkerTelemetrySummary? WorkerTelemetry => ProcessingResult.WorkerTelemetry;

    /// <summary>
    /// Pressure sample derived from processing telemetry, when processing was valid.
    /// </summary>
    public RadarProcessingPressureSample? PressureSample { get; }

    /// <summary>
    /// Direct hot-relief planner decision, when evaluated.
    /// </summary>
    public RadarProcessingRebalanceDecision? DirectHotReliefDecision { get; }

    /// <summary>
    /// Cold-evacuation planner decision, when used as fallback.
    /// </summary>
    public RadarProcessingRebalanceDecision? ColdEvacuationDecision { get; }

    /// <summary>
    /// Decision that represents the final rebalance outcome.
    /// </summary>
    public RadarProcessingRebalanceDecision? RebalanceDecision =>
        ColdEvacuationDecision ?? DirectHotReliefDecision;

    /// <summary>
    /// Migration publication result for an accepted decision.
    /// </summary>
    public RadarProcessingMigrationResult? MigrationResult { get; }

    /// <summary>
    /// State handoff validation for a published or attempted accepted move.
    /// </summary>
    public RadarProcessingStateHandoffValidationResult? HandoffValidation { get; }

    /// <summary>
    /// Quarantine lifecycle transitions emitted during the evaluation.
    /// </summary>
    public IReadOnlyList<RadarProcessingQuarantineTransition> QuarantineTransitions { get; }

    /// <summary>
    /// Snapshot of retained rebalance telemetry after the evaluation.
    /// </summary>
    public RadarProcessingRebalanceTelemetrySummary TelemetrySummary { get; }

    /// <summary>
    /// Validation profile used to validate the result.
    /// </summary>
    public RadarProcessingValidationProfile ValidationProfile { get; }

    /// <summary>
    /// Contract validation result for the session artifacts.
    /// </summary>
    public RadarProcessingRebalanceValidationResult Validation { get; }

    /// <summary>
    /// Indicates whether any planner decision was produced.
    /// </summary>
    public bool EvaluatedRebalance => RebalanceDecision is not null;

    /// <summary>
    /// Indicates whether a migration successfully published a topology move.
    /// </summary>
    public bool PublishedMigration => MigrationResult?.Succeeded == true;

    /// <summary>
    /// Indicates whether the evaluation emitted quarantine transitions.
    /// </summary>
    public bool HasQuarantineTransitions => QuarantineTransitions.Count > 0;

    private static IReadOnlyList<RadarProcessingQuarantineTransition> CopyRequired(
        IReadOnlyCollection<RadarProcessingQuarantineTransition> transitions)
    {
        if (transitions.Count == 0)
        {
            return Array.Empty<RadarProcessingQuarantineTransition>();
        }

        var result = new List<RadarProcessingQuarantineTransition>(transitions.Count);

        foreach (var transition in transitions)
        {
            ArgumentNullException.ThrowIfNull(transition);
            result.Add(transition);
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
