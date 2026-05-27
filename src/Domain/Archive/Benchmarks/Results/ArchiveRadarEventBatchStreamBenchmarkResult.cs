using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Archive;

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
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalBatches => BatchesPerIteration * Iterations;

    public long TotalEvents => EventsPerIteration * Iterations;

    public long TotalPayloadBytes => PayloadBytesPerIteration * Iterations;

    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;
}
