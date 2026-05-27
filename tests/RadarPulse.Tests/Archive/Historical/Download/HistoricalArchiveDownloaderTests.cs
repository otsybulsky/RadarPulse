using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class HistoricalArchiveDownloaderTests
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

    [Fact]
    public async Task DownloadAsyncFailsBeforeDownloadWhenFreeSpaceIsInsufficient()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = CreateFile(sizeBytes: 5, fileName: "KTLX20260504_121500_V06");
            var client = new FakeHistoricalArchiveClient([1, 2, 3, 4, 5]);
            var downloader = new HistoricalArchiveDownloader(
                client,
                new NexradCachePathMapper(),
                new FakeDiskSpaceProbe(availableBytes: 4));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                downloader.DownloadAsync(
                    new HistoricalArchiveManifest(file.ArchiveDate, [file]),
                    root,
                    maxConcurrency: 1,
                    CancellationToken.None));

            Assert.Contains("Insufficient disk space", exception.Message);
            Assert.Equal(0, client.DownloadCallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsyncDiskPreflightIgnoresExistingValidFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            var skipped = CreateFile(sizeBytes: 100, fileName: "KTLX20260504_122000_V06");
            var downloaded = CreateFile(sizeBytes: 3, fileName: "KTLX20260504_122500_V06");
            var skippedPath = new NexradCachePathMapper().MapToLocalPath(root, skipped);
            Directory.CreateDirectory(Path.GetDirectoryName(skippedPath)!);
            await File.WriteAllBytesAsync(skippedPath, Enumerable.Repeat((byte)1, 100).ToArray());

            var client = new FakeHistoricalArchiveClient([8, 9, 10]);
            var downloader = new HistoricalArchiveDownloader(
                client,
                new NexradCachePathMapper(),
                new FakeDiskSpaceProbe(availableBytes: 3));

            var result = await downloader.DownloadAsync(
                new HistoricalArchiveManifest(skipped.ArchiveDate, [skipped, downloaded]),
                root,
                maxConcurrency: 1,
                CancellationToken.None);

            Assert.Equal(1, result.DownloadedFileCount);
            Assert.Equal(1, result.SkippedFileCount);
            Assert.Equal(1, client.DownloadCallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CheckPreflightReportsRequiredAndAvailableBytes()
    {
        var root = CreateTempDirectory();
        try
        {
            var skipped = CreateFile(sizeBytes: 100, fileName: "KTLX20260504_123000_V06");
            var downloaded = CreateFile(sizeBytes: 7, fileName: "KTLX20260504_123500_V06");
            var skippedPath = new NexradCachePathMapper().MapToLocalPath(root, skipped);
            Directory.CreateDirectory(Path.GetDirectoryName(skippedPath)!);
            await File.WriteAllBytesAsync(skippedPath, Enumerable.Repeat((byte)1, 100).ToArray());

            var downloader = new HistoricalArchiveDownloader(
                new FakeHistoricalArchiveClient(),
                new NexradCachePathMapper(),
                new FakeDiskSpaceProbe(availableBytes: 999));

            var preflight = downloader.CheckPreflight(
                new HistoricalArchiveManifest(skipped.ArchiveDate, [skipped, downloaded]),
                root,
                CancellationToken.None);

            Assert.Equal(7, preflight.RequiredDownloadBytes);
            Assert.Equal(999, preflight.AvailableBytes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static HistoricalArchiveFile CreateFile(long sizeBytes, string fileName) =>
        new(
            "KTLX",
            new DateOnly(2026, 5, 4),
            $"2026/05/04/KTLX/{fileName}",
            fileName,
            sizeBytes,
            DateTimeOffset.Parse("2026-05-04T12:00:00Z"),
            new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero));

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radarpulse-download-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeHistoricalArchiveClient(params byte[] content) : IHistoricalArchiveClient
    {
        private readonly byte[] _content = content.Length == 0 ? [1, 2, 3, 4] : content;

        public int DownloadCallCount { get; private set; }

        public Task<HistoricalArchiveManifest> BuildManifestAsync(
            HistoricalArchiveRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task DownloadFileAsync(
            HistoricalArchiveFile file,
            Stream destination,
            CancellationToken cancellationToken)
        {
            DownloadCallCount++;
            await destination.WriteAsync(_content, cancellationToken);
        }
    }

    private sealed class FakeDiskSpaceProbe(long availableBytes) : IDiskSpaceProbe
    {
        public long GetAvailableBytes(string path) => availableBytes;
    }
}
