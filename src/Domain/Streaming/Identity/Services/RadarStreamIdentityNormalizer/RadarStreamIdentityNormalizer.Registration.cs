namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarStreamIdentityNormalizer
{
    private RadarStreamIdentityNormalizationResult NormalizeWithRegistration(
        ReadOnlySpan<char> radarCode,
        ReadOnlySpan<char> momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        if (!TryGetOrRegisterRadarOrdinal(radarCode, out var radarOrdinal, out var radarError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(radarError);
        }

        if (!TryGetOrRegisterMomentId(momentName, out var momentId, out var momentError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(momentError);
        }

        return CreateResolvedIdentity(
            radarOrdinal,
            momentId,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            CurrentDictionaryVersion);
    }

    private RadarStreamIdentityNormalizationResult NormalizeWithRegistration(
        ReadOnlySpan<byte> radarCodeUtf8,
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        if (!TryGetOrRegisterRadarOrdinal(radarCodeUtf8, out var radarOrdinal, out var radarError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(radarError);
        }

        if (!TryGetOrRegisterMomentId(momentNameUtf8, out var momentId, out var momentError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(momentError);
        }

        return CreateResolvedIdentity(
            radarOrdinal,
            momentId,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            CurrentDictionaryVersion);
    }

}
