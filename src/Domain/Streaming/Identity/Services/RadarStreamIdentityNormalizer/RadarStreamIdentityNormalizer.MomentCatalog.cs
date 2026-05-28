namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarStreamIdentityNormalizer
{
    private bool TryGetOrRegisterMomentId(
        ReadOnlySpan<char> momentName,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (momentCatalog.TryGetId(momentName, out momentId))
        {
            return ValidateMomentId(momentId, out error);
        }

        return TryRegisterMomentId(momentName, out momentId, out error);
    }

    private bool TryGetOrRegisterMomentId(
        ReadOnlySpan<byte> momentNameUtf8,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (momentCatalog.TryGetId(momentNameUtf8, out momentId))
        {
            return ValidateMomentId(momentId, out error);
        }

        return TryRegisterMomentId(momentNameUtf8, out momentId, out error);
    }

    private bool TryRegisterMomentId(
        ReadOnlySpan<char> momentName,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        momentId = default;

        if (!CanRegisterMomentId(out error))
        {
            return false;
        }

        var beforeCount = momentCatalog.Count;
        momentId = momentCatalog.GetOrAdd(momentName);
        return CompleteMomentIdRegistration(beforeCount, momentId, out error);
    }

    private bool TryRegisterMomentId(
        ReadOnlySpan<byte> momentNameUtf8,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        momentId = default;

        if (!CanRegisterMomentId(out error))
        {
            return false;
        }

        var beforeCount = momentCatalog.Count;
        momentId = momentCatalog.GetOrAdd(momentNameUtf8);
        return CompleteMomentIdRegistration(beforeCount, momentId, out error);
    }

    private bool CanRegisterMomentId(out RadarStreamIdentityNormalizationError error)
    {
        if (momentCatalog.Count > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.MomentIdOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private bool CompleteMomentIdRegistration(
        int beforeCount,
        int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (!ValidateMomentId(momentId, out error))
        {
            return false;
        }

        if (momentCatalog.Count != beforeCount)
        {
            AppendDictionaryVersion(radarCatalog.Count, momentCatalog.Count);
        }

        return true;
    }

    private static bool ValidateMomentId(
        int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (momentId > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.MomentIdOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }
}
