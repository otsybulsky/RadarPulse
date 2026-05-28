namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Normalizes radar code, moment name, and source dimensions to stream identities.
/// </summary>
/// <remarks>
/// The normalizer owns radar and moment dense catalogs. New catalog entries
/// advance a combined dictionary version, while source ids are resolved through
/// the fixed source universe supplied at construction.
/// </remarks>
public sealed partial class RadarStreamIdentityNormalizer
{
    private readonly DenseIdentityCatalog radarCatalog;
    private readonly DenseIdentityCatalog momentCatalog;
    private readonly RadarSourceUniverse sourceUniverse;
    private readonly object registrationGate = new();
    private readonly List<DictionaryVisibilityState> dictionaryVersions = [];
    private long dictionaryVersionValue = DictionaryVersion.Initial.Value;

    /// <summary>
    /// Creates an identity normalizer for a source universe.
    /// </summary>
    public RadarStreamIdentityNormalizer(RadarSourceUniverse sourceUniverse)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        this.sourceUniverse = sourceUniverse;
        radarCatalog = new DenseIdentityCatalog("radar", DenseIdentityCanonicalizationPolicy.RadarCode);
        momentCatalog = new DenseIdentityCatalog("moment", DenseIdentityCanonicalizationPolicy.MomentName);
        dictionaryVersions.Add(new DictionaryVisibilityState(
            DictionaryVersion.Initial,
            RadarCount: 0,
            MomentCount: 0));
    }

    /// <summary>
    /// Source universe used to resolve source ids.
    /// </summary>
    public RadarSourceUniverse SourceUniverse => sourceUniverse;

    /// <summary>
    /// Source universe version stamped onto identities.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion => sourceUniverse.Version;

    /// <summary>
    /// Current combined radar/moment dictionary version.
    /// </summary>
    public DictionaryVersion CurrentDictionaryVersion => new(Volatile.Read(ref dictionaryVersionValue));

    /// <summary>
    /// Number of visible radar catalog entries.
    /// </summary>
    public int RadarCount => radarCatalog.Count;

    /// <summary>
    /// Number of visible moment catalog entries.
    /// </summary>
    public int MomentCount => momentCatalog.Count;

}
