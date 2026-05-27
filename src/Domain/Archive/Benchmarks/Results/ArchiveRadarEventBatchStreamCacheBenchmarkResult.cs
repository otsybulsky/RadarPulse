using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Archive;

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
    public long TotalExaminedFiles => (long)ExaminedFilesPerIteration * Iterations;

    public long TotalSkippedFiles => (long)SkippedFilesPerIteration * Iterations;

    public long TotalPublishedFiles => (long)PublishedFilesPerIteration * Iterations;

    public long TotalFileSizeBytes => FileSizeBytesPerIteration * Iterations;

    public long TotalCompressedRecords => CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalBatches => BatchesPerIteration * Iterations;

    public long TotalEvents => EventsPerIteration * Iterations;

    public long TotalPayloadBytes => PayloadBytesPerIteration * Iterations;

    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;
}
