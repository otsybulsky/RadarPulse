using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class HistoricalArchiveDownloaderTests
{
    [Fact]
    public async Task DownloadAsyncSkipsExistingFileWithMatchingSizeAndBackfillsMetadata()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = CreateFile(sizeBytes: 4, fileName: "KTLX20260504_120000_V06");
            var localPath = new NexradCachePathMapper().MapToLocalPath(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await File.WriteAllBytesAsync(localPath, [1, 2, 3, 4]);

            var client = new FakeHistoricalArchiveClient();
            var downloader = new HistoricalArchiveDownloader(client, new NexradCachePathMapper());

            var result = await downloader.DownloadAsync(
                new HistoricalArchiveManifest(file.ArchiveDate, [file]),
                root,
                maxConcurrency: 2,
                CancellationToken.None);

            Assert.Equal(0, result.DownloadedFileCount);
            Assert.Equal(1, result.SkippedFileCount);
            Assert.Equal(0, client.DownloadCallCount);

            var metadataPath = $"{localPath}.metadata.json";
            Assert.True(File.Exists(metadataPath));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
            Assert.Equal(file.ArchivePath, document.RootElement.GetProperty("archivePath").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
