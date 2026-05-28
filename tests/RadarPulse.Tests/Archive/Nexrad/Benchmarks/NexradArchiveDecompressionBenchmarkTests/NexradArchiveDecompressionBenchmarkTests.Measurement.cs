using System.Buffers.Binary;
using System.Text;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveDecompressionBenchmarkTests
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
}
