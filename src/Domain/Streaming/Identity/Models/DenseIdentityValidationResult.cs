namespace RadarPulse.Domain.Streaming;

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

    public bool IsValid { get; }

    public DenseIdentityValidationError Error { get; }

    public int Length { get; }

    public int Position { get; }

    public int InvalidValue { get; }

    public DenseIdentityValidationInputKind InputKind { get; }

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
