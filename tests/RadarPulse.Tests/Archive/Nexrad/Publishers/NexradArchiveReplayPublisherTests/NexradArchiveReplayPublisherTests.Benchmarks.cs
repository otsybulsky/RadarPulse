using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    [Fact]
    public void ReplayPublishBenchmarkMeasuresStableParallelPublisherIterations()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());

        try
        {
            var result = new NexradArchiveReplayPublishBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
                {
                    [1] = firstRecordBytes,
                    [2] = secondRecordBytes
                }))
                .Measure(
                    path,
                    iterations: 2,
                    warmupIterations: 1,
                    degreeOfParallelism: 2,
                    CancellationToken.None);

            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytesPerIteration);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytesPerIteration);
            Assert.Equal(4, result.PublishedEventsPerIteration);
            Assert.Equal(4, result.ValidEventsPerIteration);
            Assert.Equal(8, result.TotalPublishedEvents);
            Assert.Equal(8, result.TotalValidEvents);
            Assert.Equal(394, result.RawValueChecksumPerIteration);
            Assert.Equal(2_000, result.CalibratedValueScaledChecksumPerIteration);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void ReplayPublishBenchmarkMeasuresStableCacheIterations()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [66, 68], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildEightBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var cachePath = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        var ktlxDirectory = Path.Combine(cachePath, "level2", "2026", "05", "04", "KTLX");
        WriteFile(
            ktlxDirectory,
            "KTLX20260504_000100_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .ToArray());
        WriteFile(ktlxDirectory, "KTLX20260504_000200_V06_MDM", [0x01, 0x02, 0x03]);
        WriteFile(
            ktlxDirectory,
            "KTLX20260504_000300_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());

        try
        {
            var result = new NexradArchiveReplayPublishBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
                {
                    [1] = firstRecordBytes,
                    [2] = secondRecordBytes
                }))
                .MeasureCache(
                    cachePath,
                    new DateOnly(2026, 5, 4),
                    "KTLX",
                    maxFiles: 3,
                    iterations: 2,
                    warmupIterations: 1,
                    degreeOfParallelism: 2,
                    CancellationToken.None);

            Assert.Equal(new DirectoryInfo(cachePath).FullName, result.CachePath);
            Assert.Equal(new DateOnly(2026, 5, 4), result.Date);
            Assert.Equal("KTLX", result.RadarId);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(3, result.ExaminedFilesPerIteration);
            Assert.Equal(1, result.SkippedFilesPerIteration);
            Assert.Equal(2, result.PublishedFilesPerIteration);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytesPerIteration);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytesPerIteration);
            Assert.Equal(4, result.PublishedEventsPerIteration);
            Assert.Equal(4, result.ValidEventsPerIteration);
            Assert.Equal(8, result.TotalPublishedEvents);
            Assert.Equal(8, result.TotalValidEvents);
            Assert.Equal(394, result.RawValueChecksumPerIteration);
            Assert.Equal(2_000, result.CalibratedValueScaledChecksumPerIteration);
            Assert.NotEqual(0UL, result.ChronologyChecksumPerIteration);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(cachePath, recursive: true);
        }
    }

}
