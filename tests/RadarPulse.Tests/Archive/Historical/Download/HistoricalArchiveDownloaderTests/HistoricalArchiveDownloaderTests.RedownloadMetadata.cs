using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class HistoricalArchiveDownloaderTests
{
    [Fact]
    public async Task DownloadAsyncRedownloadsSizeMismatchAndMovesTempFileIntoPlace()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = CreateFile(sizeBytes: 3, fileName: "KTLX20260504_120500_V06");
            var localPath = new NexradCachePathMapper().MapToLocalPath(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await File.WriteAllBytesAsync(localPath, [9, 9]);

            var client = new FakeHistoricalArchiveClient([5, 6, 7]);
            var downloader = new HistoricalArchiveDownloader(client, new NexradCachePathMapper());

            var result = await downloader.DownloadAsync(
                new HistoricalArchiveManifest(file.ArchiveDate, [file]),
                root,
                maxConcurrency: 1,
                CancellationToken.None);

            Assert.Equal(1, result.DownloadedFileCount);
            Assert.Equal(0, result.SkippedFileCount);
            Assert.Equal([5, 6, 7], await File.ReadAllBytesAsync(localPath));
            Assert.False(File.Exists($"{localPath}.part"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsyncWritesCacheMetadataAfterSuccessfulDownload()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = CreateFile(sizeBytes: 3, fileName: "KTLX20260504_120700_V06");
            var client = new FakeHistoricalArchiveClient([5, 6, 7]);
            var downloader = new HistoricalArchiveDownloader(client, new NexradCachePathMapper());

            await downloader.DownloadAsync(
                new HistoricalArchiveManifest(file.ArchiveDate, [file]),
                root,
                maxConcurrency: 1,
                CancellationToken.None);

            var localPath = new NexradCachePathMapper().MapToLocalPath(root, file);
            var metadataPath = $"{localPath}.metadata.json";
            Assert.True(File.Exists(metadataPath));
            Assert.False(File.Exists($"{metadataPath}.part"));

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
            Assert.Equal(file.ArchivePath, document.RootElement.GetProperty("archivePath").GetString());
            Assert.Equal(file.SizeBytes, document.RootElement.GetProperty("sizeBytes").GetInt64());
            Assert.Equal(file.LastModified, document.RootElement.GetProperty("lastModified").GetDateTimeOffset());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsyncRedownloadsWhenCacheMetadataDoesNotMatchManifest()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = CreateFile(sizeBytes: 3, fileName: "KTLX20260504_120800_V06");
            var localPath = new NexradCachePathMapper().MapToLocalPath(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await File.WriteAllBytesAsync(localPath, [1, 1, 1]);

            var staleMetadataFile = CreateFile(sizeBytes: 3, fileName: "KTLX20260504_120900_V06");
            await new HistoricalArchiveCacheMetadataStore().WriteAsync(
                localPath,
                staleMetadataFile,
                CancellationToken.None);

            var client = new FakeHistoricalArchiveClient([8, 9, 10]);
            var downloader = new HistoricalArchiveDownloader(client, new NexradCachePathMapper());

            var result = await downloader.DownloadAsync(
                new HistoricalArchiveManifest(file.ArchiveDate, [file]),
                root,
                maxConcurrency: 1,
                CancellationToken.None);

            Assert.Equal(1, result.DownloadedFileCount);
            Assert.Equal(0, result.SkippedFileCount);
            Assert.Equal([8, 9, 10], await File.ReadAllBytesAsync(localPath));

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync($"{localPath}.metadata.json"));
            Assert.Equal(file.ArchivePath, document.RootElement.GetProperty("archivePath").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
