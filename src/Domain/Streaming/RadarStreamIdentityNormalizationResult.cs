namespace RadarPulse.Domain.Streaming;

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

    public bool IsResolved { get; }

    public RadarStreamIdentity Identity { get; }

    public RadarStreamIdentityNormalizationError Error { get; }

    public DenseIdentityValidationResult Validation { get; }

    public static RadarStreamIdentityNormalizationResult Resolved(RadarStreamIdentity identity) =>
        new(
            isResolved: true,
            identity,
            RadarStreamIdentityNormalizationError.None,
            validation: default);

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
