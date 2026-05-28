namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingRebalanceValidator
{
    public static RadarProcessingRebalanceValidationResult ValidateSessionResult(
        RadarProcessingRebalanceSessionResult result,
        RadarProcessingTopology currentTopology)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(currentTopology);

        return ValidateSessionResult(result, currentTopology, RadarProcessingValidationProfile.Diagnostic);
    }

    /// <summary>
    /// Validates a complete rebalance session result with an explicit validation profile.
    /// </summary>
    public static RadarProcessingRebalanceValidationResult ValidateSessionResult(
        RadarProcessingRebalanceSessionResult result,
        RadarProcessingTopology currentTopology,
        RadarProcessingValidationProfile validationProfile)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(currentTopology);

        return ValidateSessionResult(
            result.ProcessingResult,
            result.PressureSample,
            result.DirectHotReliefDecision,
            result.ColdEvacuationDecision,
            result.MigrationResult,
            result.HandoffValidation,
            currentTopology,
            validationProfile);
    }

    /// <summary>
    /// Validates raw rebalance session artifacts without requiring a session result wrapper.
    /// </summary>
    public static RadarProcessingRebalanceValidationResult ValidateSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingPressureSample? pressureSample,
        RadarProcessingRebalanceDecision? directHotReliefDecision,
        RadarProcessingRebalanceDecision? coldEvacuationDecision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation,
        RadarProcessingTopology currentTopology,
        RadarProcessingValidationProfile validationProfile)
    {
        ArgumentNullException.ThrowIfNull(processingResult);
        ArgumentNullException.ThrowIfNull(currentTopology);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        var decision = coldEvacuationDecision ?? directHotReliefDecision;

        return validationProfile switch
        {
            RadarProcessingValidationProfile.Off => RadarProcessingRebalanceValidationResult.Valid(),
            RadarProcessingValidationProfile.Essential => ValidateEssentialSessionResult(
                processingResult,
                decision,
                migrationResult,
                handoffValidation,
                currentTopology),
            RadarProcessingValidationProfile.Diagnostic or RadarProcessingValidationProfile.Benchmark =>
                ValidateDiagnosticSessionResult(
                    processingResult,
                    pressureSample,
                    decision,
                    migrationResult,
                    handoffValidation,
                    currentTopology),
            _ => throw new ArgumentOutOfRangeException(nameof(validationProfile))
        };
    }
}
