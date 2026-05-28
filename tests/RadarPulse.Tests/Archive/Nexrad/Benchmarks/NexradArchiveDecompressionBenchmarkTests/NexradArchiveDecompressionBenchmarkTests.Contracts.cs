using System.Buffers.Binary;
using System.Text;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveDecompressionBenchmarkTests
{
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
}
