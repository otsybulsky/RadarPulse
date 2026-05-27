namespace RadarPulse.Domain.Processing;

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

    public bool IsValid { get; }

    public RadarProcessingRebalanceValidationError Error { get; }

    public string Message { get; }

    public RadarProcessingMigrationValidationError MigrationError { get; }

    public RadarProcessingStateHandoffValidationError HandoffError { get; }

    public static RadarProcessingRebalanceValidationResult Valid() =>
        new(
            isValid: true,
            RadarProcessingRebalanceValidationError.None,
            string.Empty,
            RadarProcessingMigrationValidationError.None,
            RadarProcessingStateHandoffValidationError.None);

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
