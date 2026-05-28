namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingRebalanceValidator
{
    private static RadarProcessingRebalanceValidationResult ValidateEssentialSessionResult(
        RadarProcessingResult processingResult,
        RadarProcessingRebalanceDecision? decision,
        RadarProcessingMigrationResult? migrationResult,
        RadarProcessingStateHandoffValidationResult? handoffValidation,
        RadarProcessingTopology currentTopology)
    {
        if (!processingResult.IsValid)
        {
            return decision is null &&
                   migrationResult is null &&
                   handoffValidation is null
                ? RadarProcessingRebalanceValidationResult.Valid()
                : Invalid(
                    RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                    "Invalid processing results must not carry rebalance artifacts.");
        }

        if (handoffValidation is not null && !handoffValidation.IsValid)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.StateHandoffValidationFailed,
                "State handoff validation failed.",
                handoffError: handoffValidation.Error);
        }

        if (migrationResult is null)
        {
            return decision?.HasAcceptedMove == true
                ? Invalid(
                    RadarProcessingRebalanceValidationError.AcceptedDecisionMissingMigration,
                    "Accepted rebalance decisions must carry a migration result.")
                : RadarProcessingRebalanceValidationResult.Valid();
        }

        if (decision?.HasAcceptedMove != true)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.UnexpectedRebalanceArtifacts,
                "Migration results require an accepted rebalance decision.");
        }

        if (!migrationResult.Succeeded)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.MigrationFailed,
                "Accepted rebalance migration failed.",
                migrationError: migrationResult.Validation.Error);
        }

        if (migrationResult.CurrentTopologyVersion.Value <= migrationResult.PreviousTopologyVersion.Value)
        {
            return Invalid(
                RadarProcessingRebalanceValidationError.NonMonotonicTopologyVersion,
                "Published migration topology version must advance.");
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
