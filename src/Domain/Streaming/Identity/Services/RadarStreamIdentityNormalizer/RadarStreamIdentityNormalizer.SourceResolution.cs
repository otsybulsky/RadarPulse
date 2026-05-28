namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarStreamIdentityNormalizer
{
    private bool TryValidateSourceDimensions(
        int elevationSlot,
        int azimuthBucket,
        int rangeBand,
        out RadarStreamIdentityNormalizationError error)
    {
        if (elevationSlot < 0 ||
            azimuthBucket < 0 ||
            rangeBand < 0 ||
            elevationSlot >= sourceUniverse.ElevationSlotCount ||
            azimuthBucket >= sourceUniverse.AzimuthBucketCount ||
            rangeBand >= sourceUniverse.RangeBandCount)
        {
            error = RadarStreamIdentityNormalizationError.SourceOutOfRange;
            return false;
        }

        if (elevationSlot > ushort.MaxValue ||
            azimuthBucket > ushort.MaxValue ||
            rangeBand > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.SourceDimensionOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private RadarStreamIdentityNormalizationResult CreateResolvedIdentity(
        int radarOrdinal,
        int momentId,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand,
        DictionaryVersion dictionaryVersion)
    {
        if (!ValidateRadarOrdinal(radarOrdinal, out var radarError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(radarError);
        }

        if (!ValidateMomentId(momentId, out var momentError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(momentError);
        }

        var key = new RadarSourceKey(radarOrdinal, elevationSlot, azimuthBucket, rangeBand);
        if (!sourceUniverse.TryGetSourceId(key, out var sourceId))
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.SourceOutOfRange);
        }

        return RadarStreamIdentityNormalizationResult.Resolved(new RadarStreamIdentity(
            sourceId,
            checked((ushort)radarOrdinal),
            checked((ushort)momentId),
            checked((ushort)elevationSlot),
            checked((ushort)azimuthBucket),
            checked((ushort)rangeBand),
            dictionaryVersion,
            sourceUniverse.Version));
    }
}
