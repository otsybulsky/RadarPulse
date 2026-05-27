namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of comparing partition state before and after owner handoff.
/// </summary>
public sealed class RadarProcessingStateHandoffValidationResult
{
    private RadarProcessingStateHandoffValidationResult(
        bool isValid,
        RadarProcessingStateHandoffValidationError error,
        RadarProcessingPartitionStateSnapshot beforeSnapshot,
        RadarProcessingPartitionStateSnapshot afterSnapshot)
    {
        if (isValid && error != RadarProcessingStateHandoffValidationError.None)
        {
            throw new ArgumentException("Valid state handoff results must not carry an error.", nameof(error));
        }

        if (!isValid && error == RadarProcessingStateHandoffValidationError.None)
        {
            throw new ArgumentException("Invalid state handoff results must carry an error.", nameof(error));
        }

        ArgumentNullException.ThrowIfNull(beforeSnapshot);
        ArgumentNullException.ThrowIfNull(afterSnapshot);

        IsValid = isValid;
        Error = error;
        BeforeSnapshot = beforeSnapshot;
        AfterSnapshot = afterSnapshot;
    }

    /// <summary>
    /// Indicates whether all compared state categories matched.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// First mismatch category, or none when valid.
    /// </summary>
    public RadarProcessingStateHandoffValidationError Error { get; }

    /// <summary>
    /// Snapshot captured before or before-projecting the handoff.
    /// </summary>
    public RadarProcessingPartitionStateSnapshot BeforeSnapshot { get; }

    /// <summary>
    /// Snapshot captured after projection or publication.
    /// </summary>
    public RadarProcessingPartitionStateSnapshot AfterSnapshot { get; }

    /// <summary>
    /// Creates a valid handoff result.
    /// </summary>
    public static RadarProcessingStateHandoffValidationResult Valid(
        RadarProcessingPartitionStateSnapshot beforeSnapshot,
        RadarProcessingPartitionStateSnapshot afterSnapshot) =>
        new(
            isValid: true,
            RadarProcessingStateHandoffValidationError.None,
            beforeSnapshot,
            afterSnapshot);

    /// <summary>
    /// Creates an invalid handoff result with an explicit mismatch category.
    /// </summary>
    public static RadarProcessingStateHandoffValidationResult Invalid(
        RadarProcessingStateHandoffValidationError error,
        RadarProcessingPartitionStateSnapshot beforeSnapshot,
        RadarProcessingPartitionStateSnapshot afterSnapshot) =>
        new(
            isValid: false,
            error,
            beforeSnapshot,
            afterSnapshot);
}
