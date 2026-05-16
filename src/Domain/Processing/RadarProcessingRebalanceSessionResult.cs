namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceSessionResult
{
    public RadarProcessingRebalanceSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingPressureSample? pressureSample,
        RadarProcessingRebalanceDecision? directHotReliefDecision,
        RadarProcessingRebalanceDecision? coldEvacuationDecision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation)
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
    }

    public RadarProcessingResult ProcessingResult { get; }

    public RadarProcessingPressureSample? PressureSample { get; }

    public RadarProcessingRebalanceDecision? DirectHotReliefDecision { get; }

    public RadarProcessingRebalanceDecision? ColdEvacuationDecision { get; }

    public RadarProcessingRebalanceDecision? RebalanceDecision =>
        ColdEvacuationDecision ?? DirectHotReliefDecision;

    public RadarProcessingMigrationResult? MigrationResult { get; }

    public RadarProcessingStateHandoffValidationResult? HandoffValidation { get; }

    public bool EvaluatedRebalance => RebalanceDecision is not null;

    public bool PublishedMigration => MigrationResult?.Succeeded == true;
}
