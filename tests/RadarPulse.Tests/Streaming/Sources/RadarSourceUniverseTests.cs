using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class RadarSourceUniverseTests
{
    [Fact]
    public void SourceCountAndStridesAreCalculatedFromDimensions()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 2,
            elevationSlotCount: 3,
            azimuthBucketCount: 4,
            rangeBandCount: 5);

        Assert.Equal(SourceUniverseVersion.Initial, universe.Version);
        Assert.Equal(2, universe.RadarOrdinalCount);
        Assert.Equal(3, universe.ElevationSlotCount);
        Assert.Equal(4, universe.AzimuthBucketCount);
        Assert.Equal(5, universe.RangeBandCount);
        Assert.Equal(5, universe.SourcesPerAzimuthBucket);
        Assert.Equal(20, universe.SourcesPerElevationSlot);
        Assert.Equal(60, universe.SourcesPerRadar);
        Assert.Equal(120, universe.SourceCount);
    }

    [Fact]
    public void SourceIdsAreDenseAcrossTheUniverse()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 2,
            elevationSlotCount: 3,
            azimuthBucketCount: 4,
            rangeBandCount: 5);
        var ids = new List<int>(universe.SourceCount);

        for (var radar = 0; radar < universe.RadarOrdinalCount; radar++)
        {
            for (var elevation = 0; elevation < universe.ElevationSlotCount; elevation++)
            {
                for (var azimuth = 0; azimuth < universe.AzimuthBucketCount; azimuth++)
                {
                    for (var band = 0; band < universe.RangeBandCount; band++)
                    {
                        ids.Add(universe.GetSourceId(new RadarSourceKey(radar, elevation, azimuth, band)));
                    }
                }
            }
        }

        Assert.Equal(universe.SourceCount, ids.Count);
        Assert.Equal(Enumerable.Range(0, universe.SourceCount), ids.Order());
        Assert.Equal(universe.SourceCount, ids.Distinct().Count());
    }

    [Fact]
    public void SourceKeyAndSourceIdRoundTrip()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 3,
            elevationSlotCount: 2,
            azimuthBucketCount: 4,
            rangeBandCount: 3);

        for (var sourceId = 0; sourceId < universe.SourceCount; sourceId++)
        {
            var key = universe.GetSourceKey(sourceId);
            Assert.Equal(sourceId, universe.GetSourceId(key));
            Assert.True(universe.TryGetSourceId(key, out var roundTripSourceId));
            Assert.Equal(sourceId, roundTripSourceId);
        }

        var explicitKey = new RadarSourceKey(
            radarOrdinal: 2,
            elevationSlot: 1,
            azimuthBucket: 3,
            rangeBand: 2);
        var explicitSourceId = universe.GetSourceId(explicitKey);
        Assert.Equal(explicitKey, universe.GetSourceKey(explicitSourceId));
    }

    [Fact]
    public void RadarOrdinalMapsToContiguousSourceIdBlock()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 3,
            elevationSlotCount: 2,
            azimuthBucketCount: 3,
            rangeBandCount: 4);

        Assert.Equal(24, universe.SourcesPerRadar);
        Assert.Equal(0, universe.GetRadarSourceBlockStart(0));
        Assert.Equal(24, universe.GetRadarSourceBlockEndExclusive(0));
        Assert.Equal(24, universe.GetRadarSourceBlockStart(1));
        Assert.Equal(48, universe.GetRadarSourceBlockEndExclusive(1));
        Assert.Equal(48, universe.GetRadarSourceBlockStart(2));
        Assert.Equal(72, universe.GetRadarSourceBlockEndExclusive(2));

        Assert.Equal(24, universe.GetSourceId(new RadarSourceKey(1, 0, 0, 0)));
        Assert.Equal(47, universe.GetSourceId(new RadarSourceKey(1, 1, 2, 3)));
    }

    [Fact]
    public void AddingRadarOrdinalKeepsExistingRadarBlockStable()
    {
        var oneRadar = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 2,
            azimuthBucketCount: 3,
            rangeBandCount: 4);
        var twoRadars = new RadarSourceUniverse(
            new SourceUniverseVersion(2),
            radarOrdinalCount: 2,
            elevationSlotCount: 2,
            azimuthBucketCount: 3,
            rangeBandCount: 4);
        var existingKey = new RadarSourceKey(0, 1, 2, 3);

        Assert.Equal(oneRadar.GetSourceId(existingKey), twoRadars.GetSourceId(existingKey));
        Assert.Equal(oneRadar.SourcesPerRadar, twoRadars.GetRadarSourceBlockStart(1));
        Assert.Equal(oneRadar.SourceCount, twoRadars.GetRadarSourceBlockStart(1));
    }

    [Fact]
    public void InvalidSourceDimensionsAreRejected()
    {
        var universe = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 2,
            elevationSlotCount: 3,
            azimuthBucketCount: 4,
            rangeBandCount: 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarSourceKey(-1, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetSourceId(new RadarSourceKey(2, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetSourceId(new RadarSourceKey(0, 3, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetSourceId(new RadarSourceKey(0, 0, 4, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetSourceId(new RadarSourceKey(0, 0, 0, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetSourceKey(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetSourceKey(universe.SourceCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => universe.GetRadarSourceBlockStart(2));

        Assert.False(universe.TryGetSourceId(new RadarSourceKey(2, 0, 0, 0), out _));
    }

    [Fact]
    public void InvalidUniverseDimensionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarSourceUniverse(
            default,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 1,
            rangeBandCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 0,
            elevationSlotCount: 1,
            azimuthBucketCount: 1,
            rangeBandCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 0,
            azimuthBucketCount: 1,
            rangeBandCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 0,
            rangeBandCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 1,
            rangeBandCount: 0));
    }

    [Fact]
    public void ChangedSourceDimensionsRequireDifferentVersion()
    {
        var original = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 2,
            azimuthBucketCount: 3,
            rangeBandCount: 4);
        var changed = new RadarSourceUniverse(
            new SourceUniverseVersion(2),
            radarOrdinalCount: 1,
            elevationSlotCount: 2,
            azimuthBucketCount: 3,
            rangeBandCount: 5);
        var invalidReuse = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 2,
            azimuthBucketCount: 3,
            rangeBandCount: 5);

        Assert.False(original.HasSameLayout(changed));
        Assert.NotEqual(original.Version, changed.Version);
        Assert.True(original.IsVersionCompatibleWith(changed));
        Assert.False(original.IsVersionCompatibleWith(invalidReuse));
    }
}
