namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceSessionResult
{
    public RadarProcessingRebalanceSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingPressureSample? pressureSample,
        RadarProcessingRebalanceDecision? directHotReliefDecision,
        RadarProcessingRebalanceDecision? coldEvacuationDecision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation,
        RadarProcessingTopology? currentTopology = null,
        IReadOnlyCollection<RadarProcessingQuarantineTransition>? quarantineTransitions = null)
    {
        ArgumentNullException.ThrowIfNull(processingResult);

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
        Validation = currentTopology is null
            ? RadarProcessingRebalanceValidationResult.Valid()
            : RadarProcessingRebalanceValidator.ValidateSessionResult(this, currentTopology);
    }

    public RadarProcessingResult ProcessingResult { get; }

    public RadarProcessingPressureSample? PressureSample { get; }

    public RadarProcessingRebalanceDecision? DirectHotReliefDecision { get; }

    public RadarProcessingRebalanceDecision? ColdEvacuationDecision { get; }

    public RadarProcessingRebalanceDecision? RebalanceDecision =>
        ColdEvacuationDecision ?? DirectHotReliefDecision;

    public RadarProcessingMigrationResult? MigrationResult { get; }

    public RadarProcessingStateHandoffValidationResult? HandoffValidation { get; }

    public IReadOnlyList<RadarProcessingQuarantineTransition> QuarantineTransitions { get; }

    public RadarProcessingRebalanceValidationResult Validation { get; }

    public bool EvaluatedRebalance => RebalanceDecision is not null;

    public bool PublishedMigration => MigrationResult?.Succeeded == true;

    public bool HasQuarantineTransitions => QuarantineTransitions.Count > 0;

    private static IReadOnlyList<RadarProcessingQuarantineTransition> CopyRequired(
        IEnumerable<RadarProcessingQuarantineTransition> transitions)
    {
        var result = new List<RadarProcessingQuarantineTransition>();

        foreach (var transition in transitions)
        {
            ArgumentNullException.ThrowIfNull(transition);
            result.Add(transition);
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
