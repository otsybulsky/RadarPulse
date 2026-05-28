using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;


public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    [Fact]
    public void StreamBenchmarkMeasuresConsistentIterations()
    {
        var firstRecordBytes = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var secondRecordBytes = BuildMessage(31, BuildSixteenBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        var benchmark = new NexradArchiveRadarEventBatchStreamBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstRecordBytes,
            [2] = secondRecordBytes
        }));

        try
        {
            var result = benchmark.Measure(
                path,
                iterations: 2,
                warmupIterations: 1,
                degreeOfParallelism: 2,
                CancellationToken.None);

            Assert.Equal(path, result.FilePath);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(StreamSchemaVersion.Current, result.StreamSchemaVersion);
            Assert.Equal(new DictionaryVersion(4), result.DictionaryVersion);
            Assert.Equal(SourceUniverseVersion.Initial, result.SourceUniverseVersion);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload1.Length + compressedPayload2.Length, result.CompressedBytesPerIteration);
            Assert.Equal(firstRecordBytes.Length + secondRecordBytes.Length, result.DecompressedBytesPerIteration);
            Assert.Equal(1, result.BatchesPerIteration);
            Assert.Equal(2, result.EventsPerIteration);
            Assert.Equal(7, result.PayloadBytesPerIteration);
            Assert.Equal(5, result.PayloadValuesPerIteration);
            Assert.Equal(266, result.RawValueChecksumPerIteration);
            Assert.Equal(1, result.RadarDictionaryEntries);
            Assert.Equal(2, result.MomentDictionaryEntries);
            Assert.NotEqual(0UL, result.DictionaryMappingChecksum);
            Assert.Equal(4, result.TotalCompressedRecords);
            Assert.Equal(4, result.TotalEvents);
            Assert.Equal(10, result.TotalPayloadValues);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void StreamCacheBenchmarkMeasuresConsistentIterations()
    {
        var firstFileFirstRecord = BuildMessage(31, BuildEightBitType31Payload("REF", [1, 2, 3], scale: 2f, offset: 66f));
        var firstFileSecondRecord = BuildMessage(31, BuildSixteenBitType31Payload("VEL", [129, 131], scale: 2f, offset: 129f));
        var secondFileRecord = BuildMessage(31, BuildEightBitType31Payload("CFP", [4, 5], scale: 1f, offset: 8f));
        var compressedPayload1 = BuildFakeBZip2Payload(1);
        var compressedPayload2 = BuildFakeBZip2Payload(2);
        var compressedPayload3 = BuildFakeBZip2Payload(3);
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload1.Length, compressedPayload1))
                .Concat(BuildCompressedRecord(compressedPayload2.Length, compressedPayload2))
                .ToArray());
        WriteTempFileInDirectory(
            directory,
            "KTLX20260504_000846_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload3.Length, compressedPayload3))
                .ToArray());
        WriteTempFileInDirectory(directory, "notes.txt", [1, 2, 3]);
        var benchmark = new NexradArchiveRadarEventBatchStreamBenchmark(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
        {
            [1] = firstFileFirstRecord,
            [2] = firstFileSecondRecord,
            [3] = secondFileRecord
        }));

        try
        {
            var result = benchmark.MeasureCache(
                directory,
                date: null,
                radarId: null,
                maxFiles: 10,
                iterations: 2,
                warmupIterations: 1,
                degreeOfParallelism: 2,
                CancellationToken.None);

            Assert.Equal(directory, result.CachePath);
            Assert.Equal("fake", result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(StreamSchemaVersion.Current, result.StreamSchemaVersion);
            Assert.Equal(SourceUniverseVersion.Initial, result.SourceUniverseVersion);
            Assert.Equal(3, result.ExaminedFilesPerIteration);
            Assert.Equal(1, result.SkippedFilesPerIteration);
            Assert.Equal(2, result.PublishedFilesPerIteration);
            Assert.Equal(3, result.CompressedRecordsPerIteration);
            Assert.Equal(
                compressedPayload1.Length + compressedPayload2.Length + compressedPayload3.Length,
                result.CompressedBytesPerIteration);
            Assert.Equal(
                firstFileFirstRecord.Length + firstFileSecondRecord.Length + secondFileRecord.Length,
                result.DecompressedBytesPerIteration);
            Assert.Equal(2, result.BatchesPerIteration);
            Assert.Equal(3, result.EventsPerIteration);
            Assert.Equal(9, result.PayloadBytesPerIteration);
            Assert.Equal(7, result.PayloadValuesPerIteration);
            Assert.Equal(275, result.RawValueChecksumPerIteration);
            Assert.Equal(6, result.TotalCompressedRecords);
            Assert.Equal(6, result.TotalEvents);
            Assert.Equal(14, result.TotalPayloadValues);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
