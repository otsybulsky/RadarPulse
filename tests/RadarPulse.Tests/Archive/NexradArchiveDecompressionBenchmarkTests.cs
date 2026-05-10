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
    [InlineData(ReusableArchiveBZip2Decompressor.DecompressorName)]
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
    public void ReusableDecompressorCountsBZip2PayloadWithLongRuns()
    {
        var original = Enumerable.Repeat((byte)'A', 1_024)
            .Concat(Encoding.ASCII.GetBytes("KTLX"))
            .Concat(Enumerable.Repeat((byte)0, 512))
            .ToArray();
        var compressedPayload = ValidBZip2PayloadWithLongRuns();
        var outputBuffer = new byte[128];

        var reusableBytes = new ReusableArchiveBZip2Decompressor()
            .CreateSession()
            .CountDecompressedBytes(compressedPayload, compressedPayload.Length, outputBuffer);
        var sharpZipLibBytes = new SharpZipLibArchiveBZip2Decompressor()
            .CreateSession()
            .CountDecompressedBytes(compressedPayload, compressedPayload.Length, outputBuffer);

        Assert.Equal(original.Length, reusableBytes);
        Assert.Equal(sharpZipLibBytes, reusableBytes);
    }

    [Fact]
    public void ReusableDecompressorStreamsChunksInOrder()
    {
        var original = Enumerable.Repeat((byte)'A', 1_024)
            .Concat(Encoding.ASCII.GetBytes("KTLX"))
            .Concat(Enumerable.Repeat((byte)0, 512))
            .ToArray();
        var compressedPayload = ValidBZip2PayloadWithLongRuns();
        var outputBuffer = new byte[7];
        using var streamed = new MemoryStream();

        var decompressedBytes = new ReusableArchiveBZip2Decompressor()
            .CreateSession()
            .Decompress(
                compressedPayload,
                compressedPayload.Length,
                outputBuffer,
                chunk => streamed.Write(chunk));

        Assert.Equal(original.Length, decompressedBytes);
        Assert.Equal(original, streamed.ToArray());
    }

    [Fact]
    public void ReusableDecompressorRejectsBZip2CrcMismatch()
    {
        var compressedPayload = ValidBZip2MetadataPayload().ToArray();
        compressedPayload[10] ^= 0x01;
        var outputBuffer = new byte[128];

        var exception = Assert.Throws<InvalidDataException>(
            () => new ReusableArchiveBZip2Decompressor()
                .CreateSession()
                .CountDecompressedBytes(compressedPayload, compressedPayload.Length, outputBuffer));

        Assert.Contains("CRC", exception.Message);
    }

    [Fact]
    public void ValidatorComparesReusableBackendAgainstSharpZipLib()
    {
        var firstPayload = ValidBZip2MetadataPayload();
        var secondPayload = ValidBZip2PayloadWithLongRuns();
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(firstPayload.Length, firstPayload))
                .Concat(BuildCompressedRecord(secondPayload.Length, secondPayload))
                .ToArray());
        try
        {
            var result = new NexradArchiveDecompressionValidator().ValidateFile(path, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Files.Select(file => file.Diagnostic)));
            Assert.Equal(1, result.ExaminedFileCount);
            Assert.Equal(0, result.SkippedFileCount);
            Assert.Equal(1, result.ComparedFileCount);
            Assert.Equal(2, result.TotalCompressedRecordCount);
            Assert.Equal(firstPayload.Length + secondPayload.Length, result.TotalCompressedBytes);
            Assert.Equal(8 + 1_540, result.TotalDecompressedBytes);
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

    private static byte[] ValidBZip2PayloadWithLongRuns() =>
    [
        0x42, 0x5A, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26,
        0x53, 0x59, 0xC4, 0x6C, 0x25, 0xCD, 0x00, 0x00,
        0x09, 0x46, 0x00, 0xC0, 0x00, 0x20, 0x0C, 0x04,
        0x40, 0x00, 0x08, 0x20, 0x00, 0x31, 0x0C, 0x00,
        0x90, 0x86, 0x9D, 0x60, 0xDA, 0xC6, 0x35, 0x02,
        0x40, 0x4E, 0xF5, 0xC3, 0x43, 0xC5, 0xDC, 0x91,
        0x4E, 0x14, 0x24, 0x31, 0x1B, 0x09, 0x73, 0x40
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
