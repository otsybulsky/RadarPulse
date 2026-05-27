namespace RadarPulse.Domain.Streaming;

public readonly record struct RadarSourceKey
{
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

    public int RadarOrdinal { get; }

    public int ElevationSlot { get; }

    public int AzimuthBucket { get; }

    public int RangeBand { get; }
}
