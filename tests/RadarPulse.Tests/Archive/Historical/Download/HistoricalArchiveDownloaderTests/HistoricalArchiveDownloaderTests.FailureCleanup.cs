using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class HistoricalArchiveDownloaderTests
{
    [Fact]
    public async Task DownloadAsyncDeletesTempFileWhenDownloadedSizeDoesNotMatchManifest()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = CreateFile(sizeBytes: 5, fileName: "KTLX20260504_121000_V06");
            var downloader = new HistoricalArchiveDownloader(
                new FakeHistoricalArchiveClient([1, 2, 3]),
                new NexradCachePathMapper());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                downloader.DownloadAsync(
                    new HistoricalArchiveManifest(file.ArchiveDate, [file]),
                    root,
                    maxConcurrency: 1,
                    CancellationToken.None));

            var localPath = new NexradCachePathMapper().MapToLocalPath(root, file);
            Assert.Contains("size mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(localPath));
            Assert.False(File.Exists($"{localPath}.part"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
