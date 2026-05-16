namespace RadarPulse.Domain.Processing;

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

    public bool IsValid { get; }

    public RadarProcessingStateHandoffValidationError Error { get; }

    public RadarProcessingPartitionStateSnapshot BeforeSnapshot { get; }

    public RadarProcessingPartitionStateSnapshot AfterSnapshot { get; }

    public static RadarProcessingStateHandoffValidationResult Valid(
        RadarProcessingPartitionStateSnapshot beforeSnapshot,
        RadarProcessingPartitionStateSnapshot afterSnapshot) =>
        new(
            isValid: true,
            RadarProcessingStateHandoffValidationError.None,
            beforeSnapshot,
            afterSnapshot);

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
