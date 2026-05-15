namespace RadarPulse.Domain.Streaming;

public readonly record struct RadarStreamIdentity
{
    public RadarStreamIdentity(
        int sourceId,
        ushort radarOrdinal,
        ushort momentId,
        ushort elevationSlot,
        ushort azimuthBucket,
        ushort rangeBand,
        DictionaryVersion dictionaryVersion,
        SourceUniverseVersion sourceUniverseVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);

        SourceId = sourceId;
        RadarOrdinal = radarOrdinal;
        MomentId = momentId;
        ElevationSlot = elevationSlot;
        AzimuthBucket = azimuthBucket;
        RangeBand = rangeBand;
        DictionaryVersion = dictionaryVersion;
        SourceUniverseVersion = sourceUniverseVersion;
    }

    public int SourceId { get; }

    public ushort RadarOrdinal { get; }

    public ushort MomentId { get; }

    public ushort ElevationSlot { get; }

    public ushort AzimuthBucket { get; }

    public ushort RangeBand { get; }

    public DictionaryVersion DictionaryVersion { get; }

    public SourceUniverseVersion SourceUniverseVersion { get; }

    public RadarSourceKey SourceKey => new(
        RadarOrdinal,
        ElevationSlot,
        AzimuthBucket,
        RangeBand);
}
