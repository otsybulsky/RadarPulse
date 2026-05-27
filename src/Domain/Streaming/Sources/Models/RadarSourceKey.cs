namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Multidimensional source key mapped to a dense source id.
/// </summary>
public readonly record struct RadarSourceKey
{
    /// <summary>
    /// Creates a source key from non-negative dimensions.
    /// </summary>
    public RadarSourceKey(
        int radarOrdinal,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radarOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegative(elevationSlot);
        ArgumentOutOfRangeException.ThrowIfNegative(azimuthBucket);
        ArgumentOutOfRangeException.ThrowIfNegative(rangeBand);

        RadarOrdinal = radarOrdinal;
        ElevationSlot = elevationSlot;
        AzimuthBucket = azimuthBucket;
        RangeBand = rangeBand;
    }

    /// <summary>
    /// Dense radar ordinal dimension.
    /// </summary>
    public int RadarOrdinal { get; }

    /// <summary>
    /// Elevation slot dimension.
    /// </summary>
    public int ElevationSlot { get; }

    /// <summary>
    /// Azimuth bucket dimension.
    /// </summary>
    public int AzimuthBucket { get; }

    /// <summary>
    /// Range band dimension.
    /// </summary>
    public int RangeBand { get; }
}
