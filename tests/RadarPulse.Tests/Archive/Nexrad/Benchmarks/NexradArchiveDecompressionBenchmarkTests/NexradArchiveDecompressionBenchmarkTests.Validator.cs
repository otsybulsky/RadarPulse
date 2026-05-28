using System.Buffers.Binary;
using System.Text;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveDecompressionBenchmarkTests
{
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
}
