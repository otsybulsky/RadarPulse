namespace RadarPulse.Domain.Streaming;

public sealed partial class RadarStreamIdentityNormalizer
{
    public RadarStreamDictionarySnapshot CreateDictionarySnapshot() =>
        CreateDictionarySnapshot(CurrentDictionaryVersion);

    /// <summary>
    /// Creates a dictionary snapshot at a previously visible combined dictionary version.
    /// </summary>
    public RadarStreamDictionarySnapshot CreateDictionarySnapshot(DictionaryVersion version)
    {
        DictionaryVisibilityState state;
        lock (registrationGate)
        {
            state = GetDictionaryVisibilityState(version);
        }

        var radarVersion = CountToCatalogVersion(state.RadarCount);
        var momentVersion = CountToCatalogVersion(state.MomentCount);
        return new RadarStreamDictionarySnapshot(
            version,
            radarCatalog.CreateSnapshot(radarVersion),
            momentCatalog.CreateSnapshot(momentVersion));
    }

    /// <summary>
    /// Attempts to look up an existing radar ordinal.
    /// </summary>
    public bool TryGetRadarOrdinal(string radarCode, out int radarOrdinal) =>
        radarCatalog.TryGetId(radarCode, out radarOrdinal);

    /// <summary>
    /// Attempts to look up an existing moment id.
    /// </summary>
    public bool TryGetMomentId(string momentName, out int momentId) =>
        momentCatalog.TryGetId(momentName, out momentId);

    private void AppendDictionaryVersion(int radarCount, int momentCount)
    {
        var nextVersion = new DictionaryVersion(checked(dictionaryVersionValue + 1));
        dictionaryVersions.Add(new DictionaryVisibilityState(nextVersion, radarCount, momentCount));
        Volatile.Write(ref dictionaryVersionValue, nextVersion.Value);
    }

    private DictionaryVisibilityState GetDictionaryVisibilityState(DictionaryVersion version)
    {
        for (var i = 0; i < dictionaryVersions.Count; i++)
        {
            if (dictionaryVersions[i].Version == version)
            {
                return dictionaryVersions[i];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(version));
    }

    private static DictionaryVersion CountToCatalogVersion(int count) =>
        new(checked(DictionaryVersion.Initial.Value + count));

    private readonly record struct DictionaryVisibilityState(
        DictionaryVersion Version,
        int RadarCount,
        int MomentCount);
}
