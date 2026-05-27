namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Result of validating dense identity text or UTF-8 bytes.
/// </summary>
public readonly record struct DenseIdentityValidationResult
{
    private DenseIdentityValidationResult(
        bool isValid,
        DenseIdentityValidationError error,
        int length,
        int position,
        int invalidValue,
        DenseIdentityValidationInputKind inputKind)
    {
        IsValid = isValid;
        Error = error;
        Length = length;
        Position = position;
        InvalidValue = invalidValue;
        InputKind = inputKind;
    }

    /// <summary>
    /// Indicates whether validation succeeded.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Validation error, or none when valid.
    /// </summary>
    public DenseIdentityValidationError Error { get; }

    /// <summary>
    /// Input length measured in chars or bytes according to input kind.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Position of the invalid value, or -1 when not position-specific.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Invalid character or byte value for invalid-character failures.
    /// </summary>
    public int InvalidValue { get; }

    /// <summary>
    /// Input representation that was validated.
    /// </summary>
    public DenseIdentityValidationInputKind InputKind { get; }

    /// <summary>
    /// Creates a valid validation result.
    /// </summary>
    public static DenseIdentityValidationResult Valid(
        int length,
        DenseIdentityValidationInputKind inputKind) =>
        new(
            isValid: true,
            DenseIdentityValidationError.None,
            length,
            position: -1,
            invalidValue: 0,
            inputKind);

    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
    public static DenseIdentityValidationResult Invalid(
        DenseIdentityValidationError error,
        int length,
        int position,
        int invalidValue,
        DenseIdentityValidationInputKind inputKind)
    {
        if (error == DenseIdentityValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        return new(
            isValid: false,
            error,
            length,
            position,
            invalidValue,
            inputKind);
    }
}
