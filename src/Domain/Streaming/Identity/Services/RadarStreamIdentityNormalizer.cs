namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Normalizes radar code, moment name, and source dimensions to stream identities.
/// </summary>
/// <remarks>
/// The normalizer owns radar and moment dense catalogs. New catalog entries
/// advance a combined dictionary version, while source ids are resolved through
/// the fixed source universe supplied at construction.
/// </remarks>
public sealed class RadarStreamIdentityNormalizer
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

    /// <summary>
    /// Normalizes canonical radar and moment strings, throwing when normalization fails.
    /// </summary>
    public RadarStreamIdentity Normalize(
        string radarCode,
        string momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var result = TryNormalize(
            radarCode,
            momentName,
            elevationSlot,
            azimuthBucket,
            rangeBand);
        if (result.IsResolved)
        {
            return result.Identity;
        }

        throw CreateNormalizationException(result);
    }

    /// <summary>
    /// Attempts to normalize canonical radar and moment strings.
    /// </summary>
    public RadarStreamIdentityNormalizationResult TryNormalize(
        string radarCode,
        string momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        ArgumentNullException.ThrowIfNull(radarCode);
        ArgumentNullException.ThrowIfNull(momentName);

        return TryNormalize(
            radarCode.AsSpan(),
            momentName.AsSpan(),
            elevationSlot,
            azimuthBucket,
            rangeBand);
    }

    /// <summary>
    /// Attempts to normalize canonical radar and moment UTF-16 spans.
    /// </summary>
    public RadarStreamIdentityNormalizationResult TryNormalize(
        ReadOnlySpan<char> radarCode,
        ReadOnlySpan<char> momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var radarValidation = radarCatalog.Validate(radarCode);
        if (!radarValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidRadarCode,
                radarValidation);
        }

        var momentValidation = momentCatalog.Validate(momentName);
        if (!momentValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidMomentName,
                momentValidation);
        }

        if (!TryValidateSourceDimensions(elevationSlot, azimuthBucket, rangeBand, out var sourceError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(sourceError);
        }

        if (radarCatalog.TryGetId(radarCode, out var radarOrdinal) &&
            momentCatalog.TryGetId(momentName, out var momentId))
        {
            return CreateResolvedIdentity(
                radarOrdinal,
                momentId,
                elevationSlot,
                azimuthBucket,
                rangeBand,
                CurrentDictionaryVersion);
        }

        lock (registrationGate)
        {
            return NormalizeWithRegistration(
                radarCode,
                momentName,
                elevationSlot,
                azimuthBucket,
                rangeBand);
        }
    }

    /// <summary>
    /// Attempts to normalize canonical radar and moment UTF-8 bytes.
    /// </summary>
    public RadarStreamIdentityNormalizationResult TryNormalize(
        ReadOnlySpan<byte> radarCodeUtf8,
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var radarValidation = radarCatalog.Validate(radarCodeUtf8);
        if (!radarValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidRadarCode,
                radarValidation);
        }

        var momentValidation = momentCatalog.Validate(momentNameUtf8);
        if (!momentValidation.IsValid)
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.InvalidMomentName,
                momentValidation);
        }

        if (!TryValidateSourceDimensions(elevationSlot, azimuthBucket, rangeBand, out var sourceError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(sourceError);
        }

        if (radarCatalog.TryGetId(radarCodeUtf8, out var radarOrdinal) &&
            momentCatalog.TryGetId(momentNameUtf8, out var momentId))
        {
            return CreateResolvedIdentity(
                radarOrdinal,
                momentId,
                elevationSlot,
                azimuthBucket,
                rangeBand,
                CurrentDictionaryVersion);
        }

        lock (registrationGate)
        {
            return NormalizeWithRegistration(
                radarCodeUtf8,
                momentNameUtf8,
                elevationSlot,
                azimuthBucket,
                rangeBand);
        }
    }

    /// <summary>
    /// Creates a dictionary snapshot at the current dictionary version.
    /// </summary>
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

    private RadarStreamIdentityNormalizationResult NormalizeWithRegistration(
        ReadOnlySpan<char> radarCode,
        ReadOnlySpan<char> momentName,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        if (!TryGetOrRegisterRadarOrdinal(radarCode, out var radarOrdinal, out var radarError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(radarError);
        }

        if (!TryGetOrRegisterMomentId(momentName, out var momentId, out var momentError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(momentError);
        }

        return CreateResolvedIdentity(
            radarOrdinal,
            momentId,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            CurrentDictionaryVersion);
    }

    private RadarStreamIdentityNormalizationResult NormalizeWithRegistration(
        ReadOnlySpan<byte> radarCodeUtf8,
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        if (!TryGetOrRegisterRadarOrdinal(radarCodeUtf8, out var radarOrdinal, out var radarError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(radarError);
        }

        if (!TryGetOrRegisterMomentId(momentNameUtf8, out var momentId, out var momentError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(momentError);
        }

        return CreateResolvedIdentity(
            radarOrdinal,
            momentId,
            elevationSlot,
            azimuthBucket,
            rangeBand,
            CurrentDictionaryVersion);
    }

    private bool TryGetOrRegisterRadarOrdinal(
        ReadOnlySpan<char> radarCode,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if (radarCatalog.TryGetId(radarCode, out radarOrdinal))
        {
            return ValidateRadarOrdinal(radarOrdinal, out error);
        }

        return TryRegisterRadarOrdinal(radarCode, out radarOrdinal, out error);
    }

    private bool TryGetOrRegisterRadarOrdinal(
        ReadOnlySpan<byte> radarCodeUtf8,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if (radarCatalog.TryGetId(radarCodeUtf8, out radarOrdinal))
        {
            return ValidateRadarOrdinal(radarOrdinal, out error);
        }

        return TryRegisterRadarOrdinal(radarCodeUtf8, out radarOrdinal, out error);
    }

    private bool TryRegisterRadarOrdinal(
        ReadOnlySpan<char> radarCode,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        radarOrdinal = default;

        if (!CanRegisterRadarOrdinal(out error))
        {
            return false;
        }

        var beforeCount = radarCatalog.Count;
        radarOrdinal = radarCatalog.GetOrAdd(radarCode);
        return CompleteRadarOrdinalRegistration(beforeCount, radarOrdinal, out error);
    }

    private bool TryRegisterRadarOrdinal(
        ReadOnlySpan<byte> radarCodeUtf8,
        out int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        radarOrdinal = default;

        if (!CanRegisterRadarOrdinal(out error))
        {
            return false;
        }

        var beforeCount = radarCatalog.Count;
        radarOrdinal = radarCatalog.GetOrAdd(radarCodeUtf8);
        return CompleteRadarOrdinalRegistration(beforeCount, radarOrdinal, out error);
    }

    private bool CanRegisterRadarOrdinal(out RadarStreamIdentityNormalizationError error)
    {
        if (radarCatalog.Count >= sourceUniverse.RadarOrdinalCount)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideSourceUniverse;
            return false;
        }

        if (radarCatalog.Count > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private bool CompleteRadarOrdinalRegistration(
        int beforeCount,
        int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if (!ValidateRadarOrdinal(radarOrdinal, out error))
        {
            return false;
        }

        if (radarCatalog.Count != beforeCount)
        {
            AppendDictionaryVersion(radarCatalog.Count, momentCatalog.Count);
        }

        return true;
    }

    private bool ValidateRadarOrdinal(
        int radarOrdinal,
        out RadarStreamIdentityNormalizationError error)
    {
        if ((uint)radarOrdinal >= (uint)sourceUniverse.RadarOrdinalCount)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideSourceUniverse;
            return false;
        }

        if (radarOrdinal > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.RadarOrdinalOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private bool TryGetOrRegisterMomentId(
        ReadOnlySpan<char> momentName,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (momentCatalog.TryGetId(momentName, out momentId))
        {
            return ValidateMomentId(momentId, out error);
        }

        return TryRegisterMomentId(momentName, out momentId, out error);
    }

    private bool TryGetOrRegisterMomentId(
        ReadOnlySpan<byte> momentNameUtf8,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (momentCatalog.TryGetId(momentNameUtf8, out momentId))
        {
            return ValidateMomentId(momentId, out error);
        }

        return TryRegisterMomentId(momentNameUtf8, out momentId, out error);
    }

    private bool TryRegisterMomentId(
        ReadOnlySpan<char> momentName,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        momentId = default;

        if (!CanRegisterMomentId(out error))
        {
            return false;
        }

        var beforeCount = momentCatalog.Count;
        momentId = momentCatalog.GetOrAdd(momentName);
        return CompleteMomentIdRegistration(beforeCount, momentId, out error);
    }

    private bool TryRegisterMomentId(
        ReadOnlySpan<byte> momentNameUtf8,
        out int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        momentId = default;

        if (!CanRegisterMomentId(out error))
        {
            return false;
        }

        var beforeCount = momentCatalog.Count;
        momentId = momentCatalog.GetOrAdd(momentNameUtf8);
        return CompleteMomentIdRegistration(beforeCount, momentId, out error);
    }

    private bool CanRegisterMomentId(out RadarStreamIdentityNormalizationError error)
    {
        if (momentCatalog.Count > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.MomentIdOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private bool CompleteMomentIdRegistration(
        int beforeCount,
        int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (!ValidateMomentId(momentId, out error))
        {
            return false;
        }

        if (momentCatalog.Count != beforeCount)
        {
            AppendDictionaryVersion(radarCatalog.Count, momentCatalog.Count);
        }

        return true;
    }

    private static bool ValidateMomentId(
        int momentId,
        out RadarStreamIdentityNormalizationError error)
    {
        if (momentId > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.MomentIdOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private bool TryValidateSourceDimensions(
        int elevationSlot,
        int azimuthBucket,
        int rangeBand,
        out RadarStreamIdentityNormalizationError error)
    {
        if (elevationSlot < 0 ||
            azimuthBucket < 0 ||
            rangeBand < 0 ||
            elevationSlot >= sourceUniverse.ElevationSlotCount ||
            azimuthBucket >= sourceUniverse.AzimuthBucketCount ||
            rangeBand >= sourceUniverse.RangeBandCount)
        {
            error = RadarStreamIdentityNormalizationError.SourceOutOfRange;
            return false;
        }

        if (elevationSlot > ushort.MaxValue ||
            azimuthBucket > ushort.MaxValue ||
            rangeBand > ushort.MaxValue)
        {
            error = RadarStreamIdentityNormalizationError.SourceDimensionOutsideStreamEventRange;
            return false;
        }

        error = RadarStreamIdentityNormalizationError.None;
        return true;
    }

    private RadarStreamIdentityNormalizationResult CreateResolvedIdentity(
        int radarOrdinal,
        int momentId,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand,
        DictionaryVersion dictionaryVersion)
    {
        if (!ValidateRadarOrdinal(radarOrdinal, out var radarError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(radarError);
        }

        if (!ValidateMomentId(momentId, out var momentError))
        {
            return RadarStreamIdentityNormalizationResult.Failed(momentError);
        }

        var key = new RadarSourceKey(radarOrdinal, elevationSlot, azimuthBucket, rangeBand);
        if (!sourceUniverse.TryGetSourceId(key, out var sourceId))
        {
            return RadarStreamIdentityNormalizationResult.Failed(
                RadarStreamIdentityNormalizationError.SourceOutOfRange);
        }

        return RadarStreamIdentityNormalizationResult.Resolved(new RadarStreamIdentity(
            sourceId,
            checked((ushort)radarOrdinal),
            checked((ushort)momentId),
            checked((ushort)elevationSlot),
            checked((ushort)azimuthBucket),
            checked((ushort)rangeBand),
            dictionaryVersion,
            sourceUniverse.Version));
    }

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

    private static ArgumentException CreateNormalizationException(
        RadarStreamIdentityNormalizationResult result) =>
        new($"Radar stream identity normalization failed: {result.Error}.");

    private readonly record struct DictionaryVisibilityState(
        DictionaryVersion Version,
        int RadarCount,
        int MomentCount);
}
