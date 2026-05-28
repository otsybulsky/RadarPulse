using System.Buffers.Binary;
using System.Text;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveDecompressionBenchmarkTests
{
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
}
