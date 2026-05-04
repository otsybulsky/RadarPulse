using RadarPulse.Domain.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class HistoricalArchiveManifestTests
{
    [Fact]
    public void SummarizeGroupsByRadar()
    {
        var files = new[]
        {
            File("KTLX", 10),
            File("KTLX", 15),
            File("KINX", 20)
        };

        var summary = new HistoricalArchiveManifest(new DateOnly(2026, 5, 4), files).Summarize();

        Assert.Equal(2, summary.RadarCount);
        Assert.Equal(3, summary.FileCount);
        Assert.Equal(45L, summary.TotalBytes);
        Assert.Equal(25L, summary.Radars.Single(r => r.RadarId == "KTLX").TotalBytes);
    }

    private static HistoricalArchiveFile File(string radarId, long sizeBytes) =>
        new(
            radarId,
            new DateOnly(2026, 5, 4),
            $"2026/05/04/{radarId}/{radarId}_20260504_120000_V06",
            $"{radarId}_20260504_120000_V06",
            sizeBytes,
            DateTimeOffset.UtcNow,
            null);
}
