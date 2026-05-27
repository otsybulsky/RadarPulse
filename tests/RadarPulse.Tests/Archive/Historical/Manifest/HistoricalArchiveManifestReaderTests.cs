using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class HistoricalArchiveManifestReaderTests
{
    [Fact]
    public async Task ReadAsyncLoadsManifestWrittenByWriter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radarpulse-manifest-{Guid.NewGuid():N}.json");
        var manifest = new HistoricalArchiveManifest(
            new DateOnly(2026, 5, 4),
            [
                new HistoricalArchiveFile(
                    "KTLX",
                    new DateOnly(2026, 5, 4),
                    "2026/05/04/KTLX/KTLX_20260504_120000_V06",
                    "KTLX_20260504_120000_V06",
                    42,
                    DateTimeOffset.Parse("2026-05-04T12:05:00Z"),
                    new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero))
            ]);

        try
        {
            var writer = new HistoricalArchiveManifestWriter();
            var reader = new HistoricalArchiveManifestReader();

            await writer.WriteAsync(manifest, path, CancellationToken.None);
            var restored = await reader.ReadAsync(path, CancellationToken.None);

            Assert.Equal(manifest.ArchiveDate, restored.ArchiveDate);
            var restoredFile = Assert.Single(restored.Files);
            Assert.Equal(manifest.Files[0], restoredFile);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
