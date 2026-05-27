namespace RadarPulse.Domain.Streaming;

public sealed class DenseIdentityCanonicalizationPolicy
{
    public static readonly DenseIdentityCanonicalizationPolicy RadarCode =
        new(
            dimensionName: "radar code",
            minimumLength: 4,
            maximumLength: 4,
            allowedCharacters:
                DenseIdentityAllowedCharacters.UppercaseAsciiLetters |
                DenseIdentityAllowedCharacters.Digits);

    public static readonly DenseIdentityCanonicalizationPolicy MomentName =
        new(
            dimensionName: "moment name",
            minimumLength: 1,
            maximumLength: 8,
            allowedCharacters:
                DenseIdentityAllowedCharacters.UppercaseAsciiLetters |
                DenseIdentityAllowedCharacters.Digits |
                DenseIdentityAllowedCharacters.Underscore);

    public DenseIdentityCanonicalizationPolicy(
        string dimensionName,
        int minimumLength,
        int maximumLength,
        DenseIdentityAllowedCharacters allowedCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionName);

        if (minimumLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        }

        if (maximumLength < minimumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        if (allowedCharacters == DenseIdentityAllowedCharacters.None)
        {
            throw new ArgumentOutOfRangeException(nameof(allowedCharacters));
        }

        DimensionName = dimensionName;
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
        AllowedCharacters = allowedCharacters;
    }

    public string DimensionName { get; }

    public int MinimumLength { get; }

    public int MaximumLength { get; }

    public DenseIdentityAllowedCharacters AllowedCharacters { get; }

    public bool TrimsInput => false;

    public bool FoldsCase => false;

    public static DenseIdentityCanonicalizationPolicy CompactIdentifier(int maximumLength = 32) =>
        new(
            dimensionName: "compact identity",
            minimumLength: 1,
            maximumLength: maximumLength,
            allowedCharacters:
                DenseIdentityAllowedCharacters.UppercaseAsciiLetters |
                DenseIdentityAllowedCharacters.Digits |
                DenseIdentityAllowedCharacters.Underscore);

    public DenseIdentityValidationResult Validate(ReadOnlySpan<char> text)
    {
        var lengthValidation = ValidateLength(text.Length, DenseIdentityValidationInputKind.Text);
        if (!lengthValidation.IsValid)
        {
            return lengthValidation;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var value = text[i];
            if (!IsAllowedAscii(value))
            {
                return DenseIdentityValidationResult.Invalid(
                    DenseIdentityValidationError.InvalidCharacter,
                    text.Length,
                    i,
                    value,
                    DenseIdentityValidationInputKind.Text);
            }
        }

        return DenseIdentityValidationResult.Valid(text.Length, DenseIdentityValidationInputKind.Text);
    }

    public DenseIdentityValidationResult Validate(ReadOnlySpan<byte> utf8Text)
    {
        var lengthValidation = ValidateLength(utf8Text.Length, DenseIdentityValidationInputKind.Utf8Bytes);
        if (!lengthValidation.IsValid)
        {
            return lengthValidation;
        }

        for (var i = 0; i < utf8Text.Length; i++)
        {
            var value = utf8Text[i];
            if (!IsAllowedAscii(value))
            {
                return DenseIdentityValidationResult.Invalid(
                    DenseIdentityValidationError.InvalidCharacter,
                    utf8Text.Length,
                    i,
                    value,
                    DenseIdentityValidationInputKind.Utf8Bytes);
            }
        }

        return DenseIdentityValidationResult.Valid(utf8Text.Length, DenseIdentityValidationInputKind.Utf8Bytes);
    }

    public bool IsCanonical(ReadOnlySpan<char> text) => Validate(text).IsValid;

    public bool IsCanonical(ReadOnlySpan<byte> utf8Text) => Validate(utf8Text).IsValid;

    public string CreateErrorMessage(
        string catalogName,
        DenseIdentityValidationResult validation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogName);

        if (validation.IsValid)
        {
            return $"Identity text for catalog '{catalogName}' and dimension '{DimensionName}' is valid.";
        }

        var input = validation.InputKind == DenseIdentityValidationInputKind.Utf8Bytes
            ? "UTF-8 identity bytes"
            : "identity text";

        return validation.Error switch
        {
            DenseIdentityValidationError.Empty =>
                $"{input} for catalog '{catalogName}' and dimension '{DimensionName}' must not be empty.",
            DenseIdentityValidationError.TooShort =>
                $"{input} for catalog '{catalogName}' and dimension '{DimensionName}' has length {validation.Length}; minimum is {MinimumLength}.",
            DenseIdentityValidationError.TooLong =>
                $"{input} for catalog '{catalogName}' and dimension '{DimensionName}' has length {validation.Length}; maximum is {MaximumLength}.",
            DenseIdentityValidationError.InvalidCharacter =>
                $"{input} for catalog '{catalogName}' and dimension '{DimensionName}' contains invalid value 0x{validation.InvalidValue:X} at position {validation.Position}; allowed characters: {AllowedCharactersDescription}.",
            _ =>
                $"{input} for catalog '{catalogName}' and dimension '{DimensionName}' is invalid."
        };
    }

    private string AllowedCharactersDescription
    {
        get
        {
            var parts = new List<string>(capacity: 3);
            if (AllowedCharacters.HasFlag(DenseIdentityAllowedCharacters.UppercaseAsciiLetters))
            {
                parts.Add("A-Z");
            }

            if (AllowedCharacters.HasFlag(DenseIdentityAllowedCharacters.Digits))
            {
                parts.Add("0-9");
            }

            if (AllowedCharacters.HasFlag(DenseIdentityAllowedCharacters.Underscore))
            {
                parts.Add("underscore");
            }

            return string.Join(", ", parts);
        }
    }

    private DenseIdentityValidationResult ValidateLength(
        int length,
        DenseIdentityValidationInputKind inputKind)
    {
        if (length == 0)
        {
            return DenseIdentityValidationResult.Invalid(
                DenseIdentityValidationError.Empty,
                length,
                position: -1,
                invalidValue: 0,
                inputKind);
        }

        if (length < MinimumLength)
        {
            return DenseIdentityValidationResult.Invalid(
                DenseIdentityValidationError.TooShort,
                length,
                position: -1,
                invalidValue: 0,
                inputKind);
        }

        if (length > MaximumLength)
        {
            return DenseIdentityValidationResult.Invalid(
                DenseIdentityValidationError.TooLong,
                length,
                position: -1,
                invalidValue: 0,
                inputKind);
        }

        return DenseIdentityValidationResult.Valid(length, inputKind);
    }

    private bool IsAllowedAscii(int value)
    {
        if (AllowedCharacters.HasFlag(DenseIdentityAllowedCharacters.UppercaseAsciiLetters) &&
            value is >= 'A' and <= 'Z')
        {
            return true;
        }

        if (AllowedCharacters.HasFlag(DenseIdentityAllowedCharacters.Digits) &&
            value is >= '0' and <= '9')
        {
            return true;
        }

        return AllowedCharacters.HasFlag(DenseIdentityAllowedCharacters.Underscore) &&
               value == '_';
    }
}
