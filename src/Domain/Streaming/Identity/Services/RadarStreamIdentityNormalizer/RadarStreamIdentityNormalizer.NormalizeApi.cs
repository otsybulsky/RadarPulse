namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarStreamIdentityNormalizer
{
    /// <summary>
    /// Normalizes canonical radar and moment strings, throwing when normalization fails.
    /// </summary>
    public RadarStreamIdentity Normalize(
        string radarCode,
        string momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var result = TryNormalize(
            radarCode,
            momentName,
            elevationSlot,
            azimuthBucket,
            rangeBand);
        if (result.IsResolved)
        {
            return result.Identity;
        }

        throw CreateNormalizationException(result);
    }

    /// <summary>
    /// Attempts to normalize canonical radar and moment strings.
    /// </summary>
    public RadarStreamIdentityNormalizationResult TryNormalize(
        string radarCode,
        string momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        ArgumentNullException.ThrowIfNull(radarCode);
        ArgumentNullException.ThrowIfNull(momentName);

        return TryNormalize(
            radarCode.AsSpan(),
            momentName.AsSpan(),
            elevationSlot,
            azimuthBucket,
            rangeBand);
    }

    /// <summary>
    /// Attempts to normalize canonical radar and moment UTF-16 spans.
    /// </summary>
    public RadarStreamIdentityNormalizationResult TryNormalize(
        ReadOnlySpan<char> radarCode,
        ReadOnlySpan<char> momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var radarValidation = radarCatalog.Validate(radarCode);
        if (!radarValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidRadarCode,
                radarValidation);
        }

        var momentValidation = momentCatalog.Validate(momentName);
        if (!momentValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidMomentName,
                momentValidation);
        }

        if (!TryValidateSourceDimensions(elevationSlot, azimuthBucket, rangeBand, out var sourceError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(sourceError);
        }

        if (radarCatalog.TryGetId(radarCode, out var radarOrdinal) &&
            momentCatalog.TryGetId(momentName, out var momentId))
        {
            return CreateResolvedIdentity(
                radarOrdinal,
                momentId,
                elevationSlot,
                azimuthBucket,
                rangeBand,
                CurrentDictionaryVersion);
        }

        lock (registrationGate)
        {
            return NormalizeWithRegistration(
                radarCode,
                momentName,
                elevationSlot,
                azimuthBucket,
                rangeBand);
        }
    }

    /// <summary>
    /// Attempts to normalize canonical radar and moment UTF-8 bytes.
    /// </summary>
    public RadarStreamIdentityNormalizationResult TryNormalize(
        ReadOnlySpan<byte> radarCodeUtf8,
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var radarValidation = radarCatalog.Validate(radarCodeUtf8);
        if (!radarValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidRadarCode,
                radarValidation);
        }

        var momentValidation = momentCatalog.Validate(momentNameUtf8);
        if (!momentValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidMomentName,
                momentValidation);
        }

        if (!TryValidateSourceDimensions(elevationSlot, azimuthBucket, rangeBand, out var sourceError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(sourceError);
        }

        if (radarCatalog.TryGetId(radarCodeUtf8, out var radarOrdinal) &&
            momentCatalog.TryGetId(momentNameUtf8, out var momentId))
        {
            return CreateResolvedIdentity(
                radarOrdinal,
                momentId,
                elevationSlot,
                azimuthBucket,
                rangeBand,
                CurrentDictionaryVersion);
        }

        lock (registrationGate)
        {
            return NormalizeWithRegistration(
                radarCodeUtf8,
                momentNameUtf8,
                elevationSlot,
                azimuthBucket,
                rangeBand);
        }
    }

    /// <summary>
    /// Creates a dictionary snapshot at the current dictionary version.
    /// </summary>

    private static ArgumentException CreateNormalizationException(
        RadarStreamIdentityNormalizationResult result) =>
        new($"Radar stream identity normalization failed: {result.Error}.");
}
