using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class HistoricalArchiveManifestSelectorTests
{
    [Fact]
    public void SelectFiltersManifestByRadarId()
    {
        var manifest = CreateManifest(
            CreateFile("KTLX", "KTLX20260504_120000_V06", 10),
            CreateFile("KOUN", "KOUN20260504_120000_V06", 10),
            CreateFile("KTLX", "KTLX20260504_120500_V06", 10));

        var selected = new HistoricalArchiveManifestSelector().Select(
            manifest,
            ["ktlx"],
            maxFiles: null,
            maxBytes: null);

        Assert.Collection(
            selected.Files,
            file => Assert.Equal("KTLX20260504_120000_V06", file.FileName),
            file => Assert.Equal("KTLX20260504_120500_V06", file.FileName));
    }

    [Fact]
    public void SelectAppliesMaxFilesAfterRadarFilter()
    {
        var manifest = CreateManifest(
            CreateFile("KOUN", "KOUN20260504_120000_V06", 10),
            CreateFile("KTLX", "KTLX20260504_120000_V06", 10),
            CreateFile("KTLX", "KTLX20260504_120500_V06", 10));

        var selected = new HistoricalArchiveManifestSelector().Select(
            manifest,
            ["KTLX"],
            maxFiles: 1,
            maxBytes: null);

        var file = Assert.Single(selected.Files);
        Assert.Equal("KTLX20260504_120000_V06", file.FileName);
    }

    [Fact]
    public void SelectStopsBeforeFileThatWouldExceedMaxBytes()
    {
        var manifest = CreateManifest(
            CreateFile("KTLX", "KTLX20260504_120000_V06", 10),
            CreateFile("KTLX", "KTLX20260504_120500_V06", 10),
            CreateFile("KTLX", "KTLX20260504_121000_V06", 10));

        var selected = new HistoricalArchiveManifestSelector().Select(
            manifest,
            ["KTLX"],
            maxFiles: null,
            maxBytes: 25);

        Assert.Equal(2, selected.Files.Count);
        Assert.Equal(20, selected.Files.Sum(file => file.SizeBytes));
    }

    [Fact]
    public void SelectKeepsOriginalManifestOrder()
    {
        var manifest = CreateManifest(
            CreateFile("KTLX", "KTLX20260504_120000_V06", 10),
            CreateFile("KOUN", "KOUN20260504_120000_V06", 10),
            CreateFile("KTLX", "KTLX20260504_120500_V06", 10));

        var selected = new HistoricalArchiveManifestSelector().Select(
            manifest,
            radarIds: null,
            maxFiles: 2,
            maxBytes: null);

        Assert.Collection(
            selected.Files,
            file => Assert.Equal("KTLX20260504_120000_V06", file.FileName),
            file => Assert.Equal("KOUN20260504_120000_V06", file.FileName));
    }

    private static HistoricalArchiveManifest CreateManifest(params HistoricalArchiveFile[] files) =>
        new(new DateOnly(2026, 5, 4), files);

    private static HistoricalArchiveFile CreateFile(string radarId, string fileName, long sizeBytes) =>
        new(
            radarId,
            new DateOnly(2026, 5, 4),
            $"2026/05/04/{radarId}/{fileName}",
            fileName,
            sizeBytes,
            DateTimeOffset.Parse("2026-05-04T12:00:00Z"),
            null);
}
