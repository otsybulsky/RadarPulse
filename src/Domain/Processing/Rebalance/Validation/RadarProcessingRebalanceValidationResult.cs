namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of validating rebalance topology, telemetry, migration, or session artifacts.
/// </summary>
public sealed class RadarProcessingRebalanceValidationResult
{
    private RadarProcessingRebalanceValidationResult(
        bool isValid,
        RadarProcessingRebalanceValidationError error,
        string message,
        RadarProcessingMigrationValidationError migrationError,
        RadarProcessingStateHandoffValidationError handoffError)
    {
        if (isValid && error != RadarProcessingRebalanceValidationError.None)
        {
            throw new ArgumentException("Valid rebalance validation results must not carry an error.", nameof(error));
        }

        if (!isValid && error == RadarProcessingRebalanceValidationError.None)
        {
            throw new ArgumentException("Invalid rebalance validation results must carry an error.", nameof(error));
        }

        if (isValid && !string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Valid rebalance validation results must not carry diagnostics.", nameof(message));
        }

        if (!isValid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        IsValid = isValid;
        Error = error;
        Message = message;
        MigrationError = migrationError;
        HandoffError = handoffError;
    }

    /// <summary>
    /// Indicates whether validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// High-level validation error, or none when valid.
    /// </summary>
    public RadarProcessingRebalanceValidationError Error { get; }

    /// <summary>
    /// Human-readable diagnostic for invalid results.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Migration validation error associated with a migration failure.
    /// </summary>
    public RadarProcessingMigrationValidationError MigrationError { get; }

    /// <summary>
    /// Handoff validation error associated with a state mismatch.
    /// </summary>
    public RadarProcessingStateHandoffValidationError HandoffError { get; }

    /// <summary>
    /// Creates a valid rebalance validation result.
    /// </summary>
    public static RadarProcessingRebalanceValidationResult Valid() =>
        new(
            isValid: true,
            RadarProcessingRebalanceValidationError.None,
            string.Empty,
            RadarProcessingMigrationValidationError.None,
            RadarProcessingStateHandoffValidationError.None);

    /// <summary>
    /// Creates an invalid rebalance validation result.
    /// </summary>
    public static RadarProcessingRebalanceValidationResult Invalid(
        RadarProcessingRebalanceValidationError error,
        string message,
        RadarProcessingMigrationValidationError migrationError = RadarProcessingMigrationValidationError.None,
        RadarProcessingStateHandoffValidationError handoffError = RadarProcessingStateHandoffValidationError.None) =>
        new(
            isValid: false,
            error,
            message,
            migrationError,
            handoffError);
}
