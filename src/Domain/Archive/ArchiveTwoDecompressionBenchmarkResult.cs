namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoDecompressionBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;
}
