namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarStreamIdentityNormalizer
{
    private bool TryGetOrRegisterRadarOrdinal(
        ReadOnlySpan<char> radarCode,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if (radarCatalog.TryGetId(radarCode, out radarOrdinal))
        {
            return ValidateRadarOrdinal(radarOrdinal, out error);
        }

        return TryRegisterRadarOrdinal(radarCode, out radarOrdinal, out error);
    }

    private bool TryGetOrRegisterRadarOrdinal(
        ReadOnlySpan<byte> radarCodeUtf8,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if (radarCatalog.TryGetId(radarCodeUtf8, out radarOrdinal))
        {
            return ValidateRadarOrdinal(radarOrdinal, out error);
        }

        return TryRegisterRadarOrdinal(radarCodeUtf8, out radarOrdinal, out error);
    }

    private bool TryRegisterRadarOrdinal(
        ReadOnlySpan<char> radarCode,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        radarOrdinal = default;

        if (!CanRegisterRadarOrdinal(out error))
        {
            return false;
        }

        var beforeCount = radarCatalog.Count;
        radarOrdinal = radarCatalog.GetOrAdd(radarCode);
        return CompleteRadarOrdinalRegistration(beforeCount, radarOrdinal, out error);
    }

    private bool TryRegisterRadarOrdinal(
        ReadOnlySpan<byte> radarCodeUtf8,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        radarOrdinal = default;

        if (!CanRegisterRadarOrdinal(out error))
        {
            return false;
        }

        var beforeCount = radarCatalog.Count;
        radarOrdinal = radarCatalog.GetOrAdd(radarCodeUtf8);
        return CompleteRadarOrdinalRegistration(beforeCount, radarOrdinal, out error);
    }

    private bool CanRegisterRadarOrdinal(out RadarStreamIdentityNormalizationError error)
    {
        if (radarCatalog.Count >= sourceUniverse.RadarOrdinalCount)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideSourceUniverse;
            return false;
        }

        if (radarCatalog.Count > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private bool CompleteRadarOrdinalRegistration(
        int beforeCount,
        int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if (!ValidateRadarOrdinal(radarOrdinal, out error))
        {
            return false;
        }

        if (radarCatalog.Count != beforeCount)
        {
            AppendDictionaryVersion(radarCatalog.Count, momentCatalog.Count);
        }

        return true;
    }

    private bool ValidateRadarOrdinal(
        int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if ((uint)radarOrdinal >= (uint)sourceUniverse.RadarOrdinalCount)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideSourceUniverse;
            return false;
        }

        if (radarOrdinal > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }
}
