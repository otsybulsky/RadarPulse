namespace RadarPulse.Domain.Streaming;

public sealed class RadarStreamDictionarySnapshot
{
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

    public DictionaryVersion Version { get; }

    public DenseIdentityCatalogSnapshot RadarCatalog { get; }

    public DenseIdentityCatalogSnapshot MomentCatalog { get; }
}
