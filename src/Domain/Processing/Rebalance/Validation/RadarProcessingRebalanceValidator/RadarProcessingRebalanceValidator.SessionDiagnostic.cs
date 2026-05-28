namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingRebalanceValidator
{
    private static RadarProcessingRebalanceValidationResult ValidateDiagnosticSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingPressureSample? pressureSample,
        RadarProcessingRebalanceDecision? decision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation,
        RadarProcessingTopology currentTopology)
    {
        if (!processingResult.IsValid)
        {
            return pressureSample is null &&
                   decision is null &&
                   migrationResult is null &&
                   handoffValidation is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Invalid processing results must not carry rebalance artifacts.");
        }

        var telemetry = processingResult.Telemetry;
        if (telemetry is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MissingTelemetry,
                "Valid rebalance processing results must carry partitioned telemetry.");
        }

        if (processingResult.TopologyVersion != telemetry.TopologyVersion)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.RouteTelemetryTopologyVersionMismatch,
                "Processing result topology version must match telemetry topology version.");
        }

        if (pressureSample is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.PressureSampleTelemetryMismatch,
                "Valid rebalance processing results must carry a pressure sample.");
        }

        var pressureValidation = ValidatePressureSample(pressureSample, telemetry);
        if (!pressureValidation.IsValid)
        {
            return pressureValidation;
        }

        if (decision is null)
        {
            return migrationResult is null && handoffValidation is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Missing rebalance decisions must not carry migration or handoff artifacts.");
        }

        if (decision.TopologyVersion != processingResult.TopologyVersion ||
            decision.TopologyVersion != pressureSample.TopologyVersion)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.DecisionTopologyVersionMismatch,
                "Rebalance decision topology version must match the processed pressure sample.");
        }

        if (handoffValidation is not null && !handoffValidation.IsValid)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.StateHandoffValidationFailed,
                "State handoff validation failed.",
                handoffError: handoffValidation.Error);
        }

        if (!decision.HasAcceptedMove)
        {
            return migrationResult is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Non-accepted rebalance decisions must not carry migration results.");
        }

        if (migrationResult is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.AcceptedDecisionMissingMigration,
                "Accepted rebalance decisions must carry a migration result.");
        }

        if (!migrationResult.Succeeded)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "Accepted rebalance migration failed.",
                migrationError: migrationResult.Validation.Error);
        }

        if (migrationResult.PreviousTopologyVersion != decision.TopologyVersion ||
            migrationResult.CurrentTopologyVersion != currentTopology.Version)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MigrationTopologyVersionMismatch,
                "Migration topology versions must match the decision and current topology.");
        }

        if (handoffValidation is null)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MissingStateHandoffValidation,
                "Published rebalance moves must carry state handoff validation.");
        }

        return RadarProcessingRebalanceValidationResult.Valid();
    }
}
