using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class HistoricalArchiveDownloaderTests
{
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
}
