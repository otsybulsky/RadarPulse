namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoParseBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    bool DecodeMomentValues,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    int MessagesPerIteration,
    int Type31RadialsPerIteration,
    long EstimatedGateMomentEventsPerIteration,
    long DecodedGateMomentValuesPerIteration,
    ulong DecodedGateMomentValueChecksumPerIteration,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalMessages => (long)MessagesPerIteration * Iterations;

    public long TotalType31Radials => (long)Type31RadialsPerIteration * Iterations;

    public long TotalEstimatedGateMomentEvents => EstimatedGateMomentEventsPerIteration * Iterations;

    public long TotalDecodedGateMomentValues => DecodedGateMomentValuesPerIteration * Iterations;
}
