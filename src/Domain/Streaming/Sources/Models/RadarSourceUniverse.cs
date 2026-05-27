namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Dense mapping between radar source dimensions and source ids.
/// </summary>
/// <remarks>
/// The layout is a Cartesian product of radar ordinal, elevation slot, azimuth
/// bucket, and range band. Source ids are stable for a layout and are used by
/// processing topology and batch routing.
/// </remarks>
public sealed class RadarSourceUniverse
{
    /// <summary>
    /// Creates a source universe layout.
    /// </summary>
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

    /// <summary>
    /// Version assigned to this source-universe layout.
    /// </summary>
    public SourceUniverseVersion Version { get; }

    /// <summary>
    /// Number of radar ordinals in the layout.
    /// </summary>
    public int RadarOrdinalCount { get; }

    /// <summary>
    /// Number of elevation slots per radar.
    /// </summary>
    public int ElevationSlotCount { get; }

    /// <summary>
    /// Number of azimuth buckets per elevation slot.
    /// </summary>
    public int AzimuthBucketCount { get; }

    /// <summary>
    /// Number of range bands per azimuth bucket.
    /// </summary>
    public int RangeBandCount { get; }

    /// <summary>
    /// Number of source ids represented by one azimuth bucket.
    /// </summary>
    public int SourcesPerAzimuthBucket { get; }

    /// <summary>
    /// Number of source ids represented by one elevation slot.
    /// </summary>
    public int SourcesPerElevationSlot { get; }

    /// <summary>
    /// Number of source ids represented by one radar ordinal.
    /// </summary>
    public int SourcesPerRadar { get; }

    /// <summary>
    /// Total number of source ids in the universe.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Maps a source key to its dense source id.
    /// </summary>
    public int GetSourceId(RadarSourceKey key)
    {
        EnsureContains(key);

        return checked(
            (key.RadarOrdinal * SourcesPerRadar) +
            (key.ElevationSlot * SourcesPerElevationSlot) +
            (key.AzimuthBucket * SourcesPerAzimuthBucket) +
            key.RangeBand);
    }

    /// <summary>
    /// Attempts to map a source key to its dense source id.
    /// </summary>
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

    /// <summary>
    /// Maps a dense source id back to its source key.
    /// </summary>
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

    /// <summary>
    /// Returns whether the source key is inside this source universe.
    /// </summary>
    public bool Contains(RadarSourceKey key) =>
        key.RadarOrdinal < RadarOrdinalCount &&
        key.ElevationSlot < ElevationSlotCount &&
        key.AzimuthBucket < AzimuthBucketCount &&
        key.RangeBand < RangeBandCount;

    /// <summary>
    /// Returns the first source id owned by a radar ordinal block.
    /// </summary>
    public int GetRadarSourceBlockStart(int radarOrdinal)
    {
        EnsureRadarOrdinal(radarOrdinal);
        return checked(radarOrdinal * SourcesPerRadar);
    }

    /// <summary>
    /// Returns the exclusive end source id for a radar ordinal block.
    /// </summary>
    public int GetRadarSourceBlockEndExclusive(int radarOrdinal) =>
        checked(GetRadarSourceBlockStart(radarOrdinal) + SourcesPerRadar);

    /// <summary>
    /// Returns whether another universe has the same dimensional layout.
    /// </summary>
    public bool HasSameLayout(RadarSourceUniverse other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return RadarOrdinalCount == other.RadarOrdinalCount &&
               ElevationSlotCount == other.ElevationSlotCount &&
               AzimuthBucketCount == other.AzimuthBucketCount &&
               RangeBandCount == other.RangeBandCount;
    }

    /// <summary>
    /// Returns whether version differences are acceptable because layouts match.
    /// </summary>
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
