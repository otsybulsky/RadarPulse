using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    [Fact]
    public void ReplayPublishSessionMatchesPublisherTotalsAcrossRepeatedParallelRuns()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [0, 1, 66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var decompressor = new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        });
        var replayPublisher = new NexradArchiveReplayPublisher(decompressor);

        try
        {
            var expected = replayPublisher.PublishFile(
                path,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);

            using var session = new NexradArchiveReplayPublishSession(decompressor, degreeOfParallelism: 2);
            var first = session.PublishFile(path, CancellationToken.None);
            var second = session.PublishFile(path, CancellationToken.None);

            Assert.Equal(2, first.DegreeOfParallelism);
            Assert.Equal(2, second.DegreeOfParallelism);
            AssertPublishResultsMatch(expected, first);
            AssertPublishResultsMatch(expected, second);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void ReplayPublishSessionRejectsUseAfterDispose()
    {
        var session = new NexradArchiveReplayPublishSession(
            new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>()),
            degreeOfParallelism: 1);

        session.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => session.PublishFile("archive", CancellationToken.None));
        session.Dispose();
    }

    [Fact]
    public void ReplayPublishSessionPublishesSelectedCacheFilesAndSkipsNonArchiveTwoFiles()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var cachePath = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        var ktlxDirectory = Path.Combine(cachePath, "level2", "2026", "05", "04", "KTLX");
        var kinxDirectory = Path.Combine(cachePath, "level2", "2026", "05", "04", "KINX");
        var nextDayDirectory = Path.Combine(cachePath, "level2", "2026", "05", "05", "KTLX");
        var firstPath = WriteFile(
            ktlxDirectory,
            "KTLX20260504_000100_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        var skippedPath = WriteFile(ktlxDirectory, "KTLX20260504_000200_V06_MDM", [0x01, 0x02, 0x03]);
        var secondPath = WriteFile(
            ktlxDirectory,
            "KTLX20260504_000300_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteFile(
            kinxDirectory,
            "KINX20260504_000400_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteFile(
            nextDayDirectory,
            "KTLX20260505_000500_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var decompressor = new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        });

        try
        {
            using var session = new NexradArchiveReplayPublishSession(decompressor, degreeOfParallelism: 2);
            var result = session.PublishCache(
                cachePath,
                new DateOnly(2026, 5, 4),
                "KTLX",
                maxFiles: 3,
                CancellationToken.None);
            var expectedFirst = new NexradArchiveReplayPublisher(decompressor).PublishFile(
                firstPath,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);
            var expectedSecond = new NexradArchiveReplayPublisher(decompressor).PublishFile(
                secondPath,
                ArchiveReplayPublishOptions.Sequential,
                CancellationToken.None);

            Assert.Equal(new DirectoryInfo(cachePath).FullName, result.CachePath);
            Assert.Equal(new DateOnly(2026, 5, 4), result.Date);
            Assert.Equal("KTLX", result.RadarId);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(3, result.ExaminedFileCount);
            Assert.Equal(1, result.SkippedFileCount);
            Assert.Equal(2, result.PublishedFileCount);
            Assert.Collection(
                result.Files,
                first => Assert.Equal(firstPath, first.FilePath),
                second => Assert.Equal(secondPath, second.FilePath));
            Assert.DoesNotContain(result.Files, file => file.FilePath == skippedPath);
            Assert.Equal(expectedFirst.FileSizeBytes + expectedSecond.FileSizeBytes, result.TotalFileSizeBytes);
            Assert.Equal(expectedFirst.CompressedRecordCount + expectedSecond.CompressedRecordCount, result.TotalCompressedRecordCount);
            Assert.Equal(expectedFirst.PublishedEvents + expectedSecond.PublishedEvents, result.TotalPublishedEvents);
            Assert.Equal(expectedFirst.ValidEvents + expectedSecond.ValidEvents, result.TotalValidEvents);
            Assert.Equal(expectedFirst.RawValueChecksum + expectedSecond.RawValueChecksum, result.TotalRawValueChecksum);
            Assert.Equal(
                expectedFirst.CalibratedValueScaledChecksum + expectedSecond.CalibratedValueScaledChecksum,
                result.TotalCalibratedValueScaledChecksum);
            Assert.NotEqual(0UL, result.ChronologyChecksum);
        }
        finally
        {
            Directory.Delete(cachePath, recursive: true);
        }
    }

}
