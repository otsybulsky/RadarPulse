namespace RadarPulse.Domain.Streaming;

public readonly record struct RadarStreamDictionarySnapshotMetrics(
    DictionaryVersion Version,
    int RadarCount,
    int MomentCount,
    ulong MappingChecksum)
{
    public static RadarStreamDictionarySnapshotMetrics Compute(RadarStreamDictionarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var checksum = RadarStreamChecksum.Initial;
        checksum = RadarStreamChecksum.AppendInt64(checksum, snapshot.Version.Value);
        checksum = AppendCatalog(checksum, snapshot.RadarCatalog);
        checksum = AppendCatalog(checksum, snapshot.MomentCatalog);

        return new RadarStreamDictionarySnapshotMetrics(
            snapshot.Version,
            snapshot.RadarCatalog.Count,
            snapshot.MomentCatalog.Count,
            checksum);
    }

    private static ulong AppendCatalog(ulong checksum, DenseIdentityCatalogSnapshot catalog)
    {
        checksum = RadarStreamChecksum.AppendStringOrdinal(checksum, catalog.Name);
        checksum = RadarStreamChecksum.AppendInt64(checksum, catalog.Version.Value);
        checksum = RadarStreamChecksum.AppendInt32(checksum, catalog.Count);

        var entries = catalog.Entries.Span;
        for (var i = 0; i < entries.Length; i++)
        {
            checksum = RadarStreamChecksum.AppendInt32(checksum, entries[i].Id);
            checksum = RadarStreamChecksum.AppendStringOrdinal(checksum, entries[i].Text);
        }

        return checksum;
    }
}
