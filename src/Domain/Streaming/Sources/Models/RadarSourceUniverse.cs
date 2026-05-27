namespace RadarPulse.Domain.Streaming;

public sealed class RadarSourceUniverse
{
    public RadarSourceUniverse(
        SourceUniverseVersion version,
        int radarOrdinalCount,
        int elevationSlotCount,
        int azimuthBucketCount,
        int rangeBandCount)
    {
        if (version.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radarOrdinalCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elevationSlotCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(azimuthBucketCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rangeBandCount);

        Version = version;
        RadarOrdinalCount = radarOrdinalCount;
        ElevationSlotCount = elevationSlotCount;
        AzimuthBucketCount = azimuthBucketCount;
        RangeBandCount = rangeBandCount;

        SourcesPerAzimuthBucket = rangeBandCount;
        SourcesPerElevationSlot = checked(azimuthBucketCount * SourcesPerAzimuthBucket);
        SourcesPerRadar = checked(elevationSlotCount * SourcesPerElevationSlot);
        SourceCount = checked(radarOrdinalCount * SourcesPerRadar);
    }

    public SourceUniverseVersion Version { get; }

    public int RadarOrdinalCount { get; }

    public int ElevationSlotCount { get; }

    public int AzimuthBucketCount { get; }

    public int RangeBandCount { get; }

    public int SourcesPerAzimuthBucket { get; }

    public int SourcesPerElevationSlot { get; }

    public int SourcesPerRadar { get; }

    public int SourceCount { get; }

    public int GetSourceId(RadarSourceKey key)
    {
        EnsureContains(key);

        return checked(
            (key.RadarOrdinal * SourcesPerRadar) +
            (key.ElevationSlot * SourcesPerElevationSlot) +
            (key.AzimuthBucket * SourcesPerAzimuthBucket) +
            key.RangeBand);
    }

    public bool TryGetSourceId(RadarSourceKey key, out int sourceId)
    {
        if (!Contains(key))
        {
            sourceId = default;
            return false;
        }

        sourceId =
            (key.RadarOrdinal * SourcesPerRadar) +
            (key.ElevationSlot * SourcesPerElevationSlot) +
            (key.AzimuthBucket * SourcesPerAzimuthBucket) +
            key.RangeBand;
        return true;
    }

    public RadarSourceKey GetSourceKey(int sourceId)
    {
        if ((uint)sourceId >= (uint)SourceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceId));
        }

        var radarOrdinal = sourceId / SourcesPerRadar;
        var radarLocal = sourceId % SourcesPerRadar;
        var elevationSlot = radarLocal / SourcesPerElevationSlot;
        var elevationLocal = radarLocal % SourcesPerElevationSlot;
        var azimuthBucket = elevationLocal / SourcesPerAzimuthBucket;
        var rangeBand = elevationLocal % SourcesPerAzimuthBucket;

        return new RadarSourceKey(radarOrdinal, elevationSlot, azimuthBucket, rangeBand);
    }

    public bool Contains(RadarSourceKey key) =>
        key.RadarOrdinal < RadarOrdinalCount &&
        key.ElevationSlot < ElevationSlotCount &&
        key.AzimuthBucket < AzimuthBucketCount &&
        key.RangeBand < RangeBandCount;

    public int GetRadarSourceBlockStart(int radarOrdinal)
    {
        EnsureRadarOrdinal(radarOrdinal);
        return checked(radarOrdinal * SourcesPerRadar);
    }

    public int GetRadarSourceBlockEndExclusive(int radarOrdinal) =>
        checked(GetRadarSourceBlockStart(radarOrdinal) + SourcesPerRadar);

    public bool HasSameLayout(RadarSourceUniverse other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return RadarOrdinalCount == other.RadarOrdinalCount &&
               ElevationSlotCount == other.ElevationSlotCount &&
               AzimuthBucketCount == other.AzimuthBucketCount &&
               RangeBandCount == other.RangeBandCount;
    }

    public bool IsVersionCompatibleWith(RadarSourceUniverse other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Version != other.Version || HasSameLayout(other);
    }

    private void EnsureContains(RadarSourceKey key)
    {
        if (Contains(key))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(key));
    }

    private void EnsureRadarOrdinal(int radarOrdinal)
    {
        if ((uint)radarOrdinal < (uint)RadarOrdinalCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(radarOrdinal));
    }
}
