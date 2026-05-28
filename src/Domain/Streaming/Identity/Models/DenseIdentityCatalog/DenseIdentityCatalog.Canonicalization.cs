namespace RadarPulse.Domain.Streaming;

public sealed partial class DenseIdentityCatalog
{
    /// <summary>
    /// Validates canonical string text without registering it.
    /// </summary>
    public DenseIdentityValidationResult Validate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Validate(text.AsSpan());
    }

    /// <summary>
    /// Validates canonical UTF-16 text without registering it.
    /// </summary>
    public DenseIdentityValidationResult Validate(ReadOnlySpan<char> text) =>
        canonicalizationPolicy.Validate(text);

    /// <summary>
    /// Validates canonical UTF-8 bytes without registering them.
    /// </summary>
    public DenseIdentityValidationResult Validate(ReadOnlySpan<byte> utf8Text) =>
        canonicalizationPolicy.Validate(utf8Text);

    private void EnsureCanonical(ReadOnlySpan<char> text)
    {
        var validation = canonicalizationPolicy.Validate(text);
        if (validation.IsValid)
        {
            return;
        }

        throw new ArgumentException(canonicalizationPolicy.CreateErrorMessage(Name, validation));
    }

    private void EnsureCanonicalUtf8(ReadOnlySpan<byte> text)
    {
        var validation = canonicalizationPolicy.Validate(text);
        if (validation.IsValid)
        {
            return;
        }

        throw new ArgumentException(canonicalizationPolicy.CreateErrorMessage(Name, validation));
    }
}
