namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Normalized source and dictionary identity for one radar stream event.
/// </summary>
public readonly record struct RadarStreamIdentity
{
    /// <summary>
    /// Creates a normalized radar stream identity.
    /// </summary>
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

    /// <summary>
    /// Dense source id inside the source universe.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Dense radar code ordinal from the radar catalog.
    /// </summary>
    public ushort RadarOrdinal { get; }

    /// <summary>
    /// Dense moment id from the moment catalog.
    /// </summary>
    public ushort MomentId { get; }

    /// <summary>
    /// Elevation slot dimension.
    /// </summary>
    public ushort ElevationSlot { get; }

    /// <summary>
    /// Azimuth bucket dimension.
    /// </summary>
    public ushort AzimuthBucket { get; }

    /// <summary>
    /// Range band dimension.
    /// </summary>
    public ushort RangeBand { get; }

    /// <summary>
    /// Dictionary version that made the radar and moment ids visible.
    /// </summary>
    public DictionaryVersion DictionaryVersion { get; }

    /// <summary>
    /// Source universe version used to calculate the source id.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion { get; }

    /// <summary>
    /// Source key represented by the identity dimensions.
    /// </summary>
    public RadarSourceKey SourceKey => new(
        RadarOrdinal,
        ElevationSlot,
        AzimuthBucket,
        RangeBand);
}
