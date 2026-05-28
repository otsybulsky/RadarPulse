namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveDecompressionBenchmark
{
    private readonly record struct ArchiveTwoIterationMeasurement(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes);
}
