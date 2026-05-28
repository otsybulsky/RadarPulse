namespace RadarPulse.Domain.Processing;

/// <summary>
/// Contract validator for rebalance topology, telemetry, pressure, and session artifacts.
/// </summary>
/// <remarks>
/// Validation is used as executable documentation for the rebalance contract:
/// topology shape is stable, versions advance monotonically, route telemetry
/// matches topology ownership, and accepted moves carry migration plus state
/// handoff evidence.
/// </remarks>
public static partial class RadarProcessingRebalanceValidator
{
    private static RadarProcessingRebalanceValidationResult Invalid(
        RadarProcessingRebalanceValidationError error,
        string message,
        RadarProcessingMigrationValidationError migrationError = RadarProcessingMigrationValidationError.None,
        RadarProcessingStateHandoffValidationError handoffError = RadarProcessingStateHandoffValidationError.None) =>
        RadarProcessingRebalanceValidationResult.Invalid(
            error,
            message,
            migrationError,
            handoffError);
}
