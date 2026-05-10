using System.Buffers.Binary;
using System.Text;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class NexradArchiveDecompressionBenchmarkTests
{
    [Fact]
    public void MeasureReportsStablePerIterationTotals()
    {
        var compressedPayload = ValidBZip2MetadataPayload();
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        try
        {
            var result = new NexradArchiveDecompressionBenchmark().Measure(path, 2, 1, CancellationToken.None);

            Assert.Equal(path, result.FilePath);
            Assert.Equal(ArchiveBZip2Decompressors.DefaultName, result.Decompressor);
            Assert.Equal(2, result.Iterations);
            Assert.Equal(1, result.WarmupIterations);
            Assert.Equal(1, result.DegreeOfParallelism);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload.Length * 2L, result.CompressedBytesPerIteration);
            Assert.Equal(16, result.DecompressedBytesPerIteration);
            Assert.Equal(4, result.TotalCompressedRecords);
            Assert.Equal(compressedPayload.Length * 4L, result.TotalCompressedBytes);
            Assert.Equal(32, result.TotalDecompressedBytes);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Theory]
    [InlineData(SharpCompressArchiveBZip2Decompressor.DecompressorName)]
    [InlineData(SharpZipLibArchiveBZip2Decompressor.DecompressorName)]
    public void MeasureSupportsConfiguredDecompressor(string decompressor)
    {
        var compressedPayload = ValidBZip2MetadataPayload();
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        try
        {
            var result = new NexradArchiveDecompressionBenchmark().Measure(
                path,
                1,
                0,
                2,
                decompressor,
                CancellationToken.None);

            Assert.Equal(decompressor, result.Decompressor);
            Assert.Equal(2, result.DegreeOfParallelism);
            Assert.Equal(2, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload.Length * 2L, result.CompressedBytesPerIteration);
            Assert.Equal(16, result.DecompressedBytesPerIteration);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void MeasureSupportsParallelDecompression()
    {
        var compressedPayload = ValidBZip2MetadataPayload();
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        try
        {
            var result = new NexradArchiveDecompressionBenchmark().Measure(path, 2, 0, 3, CancellationToken.None);

            Assert.Equal(3, result.DegreeOfParallelism);
            Assert.Equal(3, result.CompressedRecordsPerIteration);
            Assert.Equal(compressedPayload.Length * 3L, result.CompressedBytesPerIteration);
            Assert.Equal(24, result.DecompressedBytesPerIteration);
            Assert.Equal(6, result.TotalCompressedRecords);
            Assert.Equal(compressedPayload.Length * 6L, result.TotalCompressedBytes);
            Assert.Equal(48, result.TotalDecompressedBytes);
            Assert.True(result.Elapsed > TimeSpan.Zero);
            Assert.True(result.AllocatedBytes > 0);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void MeasureRejectsNonArchiveTwoFile()
    {
        var path = WriteTempFile("unknown", [0x01, 0x02, 0x03, 0x04]);
        try
        {
            var exception = Assert.Throws<InvalidDataException>(
                () => new NexradArchiveDecompressionBenchmark().Measure(path, 1, 0, CancellationToken.None));

            Assert.Contains("Archive Two volume header", exception.Message);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void MeasureRejectsInvalidParallelism()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new NexradArchiveDecompressionBenchmark().Measure("archive", 1, 0, 0, CancellationToken.None));

        Assert.Contains("parallelism", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeasureRejectsUnknownDecompressor()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new NexradArchiveDecompressionBenchmark().Measure(
                "archive",
                1,
                0,
                1,
                "unknown",
                CancellationToken.None));

        Assert.Contains("Unknown decompressor", exception.Message);
    }

    private static byte[] BuildArchiveTwoHeader()
    {
        var header = new byte[24];
        Encoding.ASCII.GetBytes("AR2V0006.266").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(
            header.AsSpan(12, 4),
            new DateOnly(2026, 5, 4).DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), 164_018);
        Encoding.ASCII.GetBytes("KTLX").CopyTo(header, 20);
        return header;
    }

    private static byte[] BuildCompressedRecord(int controlWord, byte[] compressedPayload) =>
        BuildCompressedRecordControlWord(controlWord).Concat(compressedPayload).ToArray();

    private static byte[] BuildCompressedRecordControlWord(int controlWord)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, controlWord);
        return buffer;
    }

    private static byte[] ValidBZip2MetadataPayload() =>
    [
        0x42, 0x5A, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26,
        0x53, 0x59, 0x01, 0xFE, 0xBF, 0xBC, 0x00, 0x00,
        0x02, 0x81, 0x80, 0x26, 0x02, 0x04, 0x00, 0x20,
        0x00, 0x30, 0xCD, 0x00, 0xC1, 0xA0, 0xAD, 0x21,
        0x38, 0xBB, 0x92, 0x29, 0xC2, 0x84, 0x80, 0x0F,
        0xF5, 0xFD, 0xE0
    ];

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }
}
