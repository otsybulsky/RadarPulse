namespace RadarPulse.Domain.Archive;

/// <summary>
/// Timing, allocation, and byte-count result for repeated Archive II BZip2 decompression.
/// </summary>
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
    /// <summary>
    /// Gets total compressed records processed across measured iterations.
    /// </summary>
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    /// <summary>
    /// Gets total compressed payload bytes processed across measured iterations.
    /// </summary>
    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total decompressed bytes produced across measured iterations.
    /// </summary>
    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;
}
