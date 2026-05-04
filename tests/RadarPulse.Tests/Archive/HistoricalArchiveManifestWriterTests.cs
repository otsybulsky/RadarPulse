using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class HistoricalArchiveManifestWriterTests
{
    [Fact]
    public async Task WriteAsyncPersistsManifestJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radarpulse-manifest-{Guid.NewGuid():N}.json");
        var manifest = new HistoricalArchiveManifest(
            new DateOnly(2026, 5, 4),
            NexradArchiveKey.BucketName,
            [
                new HistoricalArchiveFile(
                    "KTLX",
                    new DateOnly(2026, 5, 4),
                    NexradArchiveKey.BucketName,
                    "2026/05/04/KTLX/KTLX_20260504_120000_V06",
                    "KTLX_20260504_120000_V06",
                    42,
                    DateTimeOffset.Parse("2026-05-04T12:05:00Z"),
                    new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero))
            ]);

        try
        {
            await new HistoricalArchiveManifestWriter().WriteAsync(manifest, path, CancellationToken.None);

            var json = await File.ReadAllTextAsync(path);
            using var document = JsonDocument.Parse(json);

            Assert.Equal("2026-05-04", document.RootElement.GetProperty("archiveDate").GetString());
            Assert.Equal(NexradArchiveKey.BucketName, document.RootElement.GetProperty("bucket").GetString());
            Assert.Single(document.RootElement.GetProperty("files").EnumerateArray());
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
