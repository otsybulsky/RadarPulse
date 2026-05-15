using System.Text;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class RadarStreamIdentityNormalizerTests
{
    [Fact]
    public void ValidIdentityResolvesDenseIdsAndVersions()
    {
        var universe = CreateUniverse(radarCount: 2);
        var normalizer = new RadarStreamIdentityNormalizer(universe);

        var result = normalizer.TryNormalize(
            "KTLX",
            "REF",
            elevationSlot: 2,
            azimuthBucket: 180,
            rangeBand: 1);

        Assert.True(result.IsResolved);
        var identity = result.Identity;
        Assert.Equal(0, identity.RadarOrdinal);
        Assert.Equal(0, identity.MomentId);
        Assert.Equal(2, identity.ElevationSlot);
        Assert.Equal(180, identity.AzimuthBucket);
        Assert.Equal(1, identity.RangeBand);
        Assert.Equal(new DictionaryVersion(3), identity.DictionaryVersion);
        Assert.Equal(universe.Version, identity.SourceUniverseVersion);
        Assert.Equal(
            universe.GetSourceId(new RadarSourceKey(0, 2, 180, 1)),
            identity.SourceId);
    }

    [Fact]
    public void RepeatedIdentityDoesNotAdvanceDictionaryVersion()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var first = normalizer.Normalize("KTLX", "REF", 0, 0, 0);
        var second = normalizer.Normalize("KTLX", "REF", 0, 0, 0);

        Assert.Equal(first.DictionaryVersion, second.DictionaryVersion);
        Assert.Equal(first.SourceId, second.SourceId);
        Assert.Equal(1, normalizer.RadarCount);
        Assert.Equal(1, normalizer.MomentCount);
    }

    [Fact]
    public void UnknownValidIdentitiesAppendThroughColdPath()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var first = normalizer.Normalize("KTLX", "REF", 0, 0, 0);
        var second = normalizer.Normalize("KOUN", "VEL", 0, 0, 0);

        Assert.Equal(0, first.RadarOrdinal);
        Assert.Equal(0, first.MomentId);
        Assert.Equal(1, second.RadarOrdinal);
        Assert.Equal(1, second.MomentId);
        Assert.Equal(new DictionaryVersion(5), second.DictionaryVersion);
        Assert.Equal(2, normalizer.RadarCount);
        Assert.Equal(2, normalizer.MomentCount);
    }

    [Fact]
    public void DictionarySnapshotForResolutionVersionResolvesPublishedIds()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));
        var identity = normalizer.Normalize("KTLX", "REF", 0, 0, 0);

        var snapshot = normalizer.CreateDictionarySnapshot(identity.DictionaryVersion);

        Assert.Equal(identity.DictionaryVersion, snapshot.Version);
        Assert.True(snapshot.RadarCatalog.TryGetText(identity.RadarOrdinal, out var radarCode));
        Assert.True(snapshot.MomentCatalog.TryGetText(identity.MomentId, out var momentName));
        Assert.Equal("KTLX", radarCode);
        Assert.Equal("REF", momentName);
    }

    [Fact]
    public void InvalidRadarCodeDoesNotRegisterIdentity()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var result = normalizer.TryNormalize("ktlx", "REF", 0, 0, 0);

        Assert.False(result.IsResolved);
        Assert.Equal(RadarStreamIdentityNormalizationError.InvalidRadarCode, result.Error);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, result.Validation.Error);
        Assert.Equal(0, normalizer.RadarCount);
        Assert.Equal(0, normalizer.MomentCount);
        Assert.Equal(DictionaryVersion.Initial, normalizer.CurrentDictionaryVersion);
    }

    [Fact]
    public void InvalidMomentNameDoesNotRegisterRadarOrMoment()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var result = normalizer.TryNormalize("KTLX", "ref", 0, 0, 0);

        Assert.False(result.IsResolved);
        Assert.Equal(RadarStreamIdentityNormalizationError.InvalidMomentName, result.Error);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, result.Validation.Error);
        Assert.Equal(0, normalizer.RadarCount);
        Assert.Equal(0, normalizer.MomentCount);
        Assert.Equal(DictionaryVersion.Initial, normalizer.CurrentDictionaryVersion);
    }

    [Fact]
    public void SourceTupleOutsideUniverseDoesNotRegisterIdentities()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var result = normalizer.TryNormalize(
            "KTLX",
            "REF",
            elevationSlot: 12,
            azimuthBucket: 0,
            rangeBand: 0);

        Assert.False(result.IsResolved);
        Assert.Equal(RadarStreamIdentityNormalizationError.SourceOutOfRange, result.Error);
        Assert.Equal(0, normalizer.RadarCount);
        Assert.Equal(0, normalizer.MomentCount);
    }

    [Fact]
    public void RadarOutsideSourceUniverseCapacityDoesNotAppend()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 1));
        normalizer.Normalize("KTLX", "REF", 0, 0, 0);

        var result = normalizer.TryNormalize("KOUN", "VEL", 0, 0, 0);

        Assert.False(result.IsResolved);
        Assert.Equal(RadarStreamIdentityNormalizationError.RadarOrdinalOutsideSourceUniverse, result.Error);
        Assert.False(normalizer.TryGetRadarOrdinal("KOUN", out _));
        Assert.False(normalizer.TryGetMomentId("VEL", out _));
        Assert.Equal(1, normalizer.RadarCount);
        Assert.Equal(1, normalizer.MomentCount);
        Assert.Equal(new DictionaryVersion(3), normalizer.CurrentDictionaryVersion);
    }

    [Fact]
    public void Utf8InputUsesSameMappingsAsTextInput()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var textIdentity = normalizer.Normalize("KTLX", "REF", 0, 0, 0);
        var utf8Result = normalizer.TryNormalize(
            Encoding.ASCII.GetBytes("KTLX"),
            Encoding.ASCII.GetBytes("REF"),
            elevationSlot: 0,
            azimuthBucket: 0,
            rangeBand: 0);

        Assert.True(utf8Result.IsResolved);
        Assert.Equal(textIdentity.RadarOrdinal, utf8Result.Identity.RadarOrdinal);
        Assert.Equal(textIdentity.MomentId, utf8Result.Identity.MomentId);
        Assert.Equal(textIdentity.DictionaryVersion, utf8Result.Identity.DictionaryVersion);
    }

    [Fact]
    public void NormalizeThrowsForFailedResolution()
    {
        var normalizer = new RadarStreamIdentityNormalizer(CreateUniverse(radarCount: 2));

        var exception = Assert.Throws<ArgumentException>(() =>
            normalizer.Normalize("KTLX", "REF", elevationSlot: -1, azimuthBucket: 0, rangeBand: 0));

        Assert.Contains(
            nameof(RadarStreamIdentityNormalizationError.SourceOutOfRange),
            exception.Message,
            StringComparison.Ordinal);
    }

    private static RadarSourceUniverse CreateUniverse(int radarCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: radarCount,
            elevationSlotCount: 12,
            azimuthBucketCount: 720,
            rangeBandCount: 3);
}
