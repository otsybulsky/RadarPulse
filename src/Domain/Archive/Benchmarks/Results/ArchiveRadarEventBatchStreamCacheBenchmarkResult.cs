using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Archive;

/// <summary>
/// Benchmark result for projecting a cache selection into radar event batches.
/// </summary>
public sealed record ArchiveRadarEventBatchStreamCacheBenchmarkResult(
    string CachePath,
    DateOnly? Date,
    string? RadarId,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    StreamSchemaVersion StreamSchemaVersion,
    SourceUniverseVersion SourceUniverseVersion,
    int ExaminedFilesPerIteration,
    int SkippedFilesPerIteration,
    int PublishedFilesPerIteration,
    long FileSizeBytesPerIteration,
    long CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long BatchesPerIteration,
    long EventsPerIteration,
    long PayloadBytesPerIteration,
    long PayloadValuesPerIteration,
    long RawValueChecksumPerIteration,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    /// <summary>
    /// Gets total examined files across measured iterations.
    /// </summary>
    public long TotalExaminedFiles => (long)ExaminedFilesPerIteration * Iterations;

    /// <summary>
    /// Gets total skipped files across measured iterations.
    /// </summary>
    public long TotalSkippedFiles => (long)SkippedFilesPerIteration * Iterations;

    /// <summary>
    /// Gets total published files across measured iterations.
    /// </summary>
    public long TotalPublishedFiles => (long)PublishedFilesPerIteration * Iterations;

    /// <summary>
    /// Gets total file size bytes across measured iterations.
    /// </summary>
    public long TotalFileSizeBytes => FileSizeBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total compressed records across measured iterations.
    /// </summary>
    public long TotalCompressedRecords => CompressedRecordsPerIteration * Iterations;

    /// <summary>
    /// Gets total compressed payload bytes across measured iterations.
    /// </summary>
    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total decompressed bytes across measured iterations.
    /// </summary>
    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total radar event batches produced across measured iterations.
    /// </summary>
    public long TotalBatches => BatchesPerIteration * Iterations;

    /// <summary>
    /// Gets total radar stream events produced across measured iterations.
    /// </summary>
    public long TotalEvents => EventsPerIteration * Iterations;

    /// <summary>
    /// Gets total payload bytes produced across measured iterations.
    /// </summary>
    public long TotalPayloadBytes => PayloadBytesPerIteration * Iterations;

    /// <summary>
    /// Gets total decoded payload values produced across measured iterations.
    /// </summary>
    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;
}
