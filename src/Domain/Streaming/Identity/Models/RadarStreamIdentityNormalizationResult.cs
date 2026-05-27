namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Result of normalizing radar/moment/source dimensions to a stream identity.
/// </summary>
public readonly record struct RadarStreamIdentityNormalizationResult
{
    private RadarStreamIdentityNormalizationResult(
        bool isResolved,
        RadarStreamIdentity identity,
        RadarStreamIdentityNormalizationError error,
        DenseIdentityValidationResult validation)
    {
        IsResolved = isResolved;
        Identity = identity;
        Error = error;
        Validation = validation;
    }

    /// <summary>
    /// Indicates whether normalization produced an identity.
    /// </summary>
    public bool IsResolved { get; }

    /// <summary>
    /// Resolved identity when normalization succeeds.
    /// </summary>
    public RadarStreamIdentity Identity { get; }

    /// <summary>
    /// Failure category when normalization fails.
    /// </summary>
    public RadarStreamIdentityNormalizationError Error { get; }

    /// <summary>
    /// Dense identity validation detail for text failures.
    /// </summary>
    public DenseIdentityValidationResult Validation { get; }

    /// <summary>
    /// Creates a successful normalization result.
    /// </summary>
    public static RadarStreamIdentityNormalizationResult Resolved(RadarStreamIdentity identity) =>
        new(
            isResolved: true,
            identity,
            RadarStreamIdentityNormalizationError.None,
            validation: default);

    /// <summary>
    /// Creates a failed normalization result.
    /// </summary>
    public static RadarStreamIdentityNormalizationResult Failed(
        RadarStreamIdentityNormalizationError error,
        DenseIdentityValidationResult validation = default)
    {
        if (error == RadarStreamIdentityNormalizationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        return new(
            isResolved: false,
            identity: default,
            error,
            validation);
    }
}
