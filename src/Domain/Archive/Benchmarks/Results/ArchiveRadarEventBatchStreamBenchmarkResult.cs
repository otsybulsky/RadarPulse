using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Archive;

/// <summary>
/// Benchmark result for projecting one Archive II file into radar event batches.
/// </summary>
public sealed record ArchiveRadarEventBatchStreamBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    StreamSchemaVersion StreamSchemaVersion,
    DictionaryVersion DictionaryVersion,
    SourceUniverseVersion SourceUniverseVersion,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long BatchesPerIteration,
    long EventsPerIteration,
    long PayloadBytesPerIteration,
    long PayloadValuesPerIteration,
    long RawValueChecksumPerIteration,
    int RadarDictionaryEntries,
    int MomentDictionaryEntries,
    ulong DictionaryMappingChecksum,
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
