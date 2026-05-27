namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Combined radar and moment dictionary snapshot for a stream dictionary version.
/// </summary>
public sealed class RadarStreamDictionarySnapshot
{
    /// <summary>
    /// Creates a combined stream dictionary snapshot.
    /// </summary>
    public RadarStreamDictionarySnapshot(
        DictionaryVersion version,
        DenseIdentityCatalogSnapshot radarCatalog,
        DenseIdentityCatalogSnapshot momentCatalog)
    {
        ArgumentNullException.ThrowIfNull(radarCatalog);
        ArgumentNullException.ThrowIfNull(momentCatalog);

        Version = version;
        RadarCatalog = radarCatalog;
        MomentCatalog = momentCatalog;
    }

    /// <summary>
    /// Combined dictionary version represented by the snapshot.
    /// </summary>
    public DictionaryVersion Version { get; }

    /// <summary>
    /// Radar code catalog snapshot.
    /// </summary>
    public DenseIdentityCatalogSnapshot RadarCatalog { get; }

    /// <summary>
    /// Moment name catalog snapshot.
    /// </summary>
    public DenseIdentityCatalogSnapshot MomentCatalog { get; }
}
