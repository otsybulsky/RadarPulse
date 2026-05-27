using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingArchiveOrderedProcessingBenchmarkResult(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    string Decompressor,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    int SourceCount,
    int PartitionCount,
    int ShardCount,
    int ActiveBatchCapacity,
    long ExaminedFilesPerIteration,
    long SkippedFilesPerIteration,
    long PublishedFilesPerIteration,
    long FileSizeBytesPerIteration,
    long CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long BatchesPerIteration,
    long EventsPerIteration,
    long PayloadBytesPerIteration,
    long PayloadValuesPerIteration,
    long RawValueChecksumPerIteration,
    RadarProcessingArchiveQueuedOverlapStatus Status,
    RadarProcessingQueuedSessionStatus ConsumerStatus,
    long SucceededBatchCount,
    long FailedProcessingBatchCount,
    long FailedValidationBatchCount,
    long CanceledBatchCount,
    long SkippedAfterFaultBatchCount,
    long FinalProcessedBatchCount,
    long FinalProcessedStreamEventCount,
    long FinalProcessedPayloadValueCount,
    long FinalRawValueChecksum,
    ulong FinalProcessingChecksum,
    bool ProcessingSucceeded,
    TimeSpan Elapsed,
    long AllocatedBytes,
    RadarProcessingProviderQueueTelemetrySummary QueueTelemetry,
    RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry,
    RadarProcessingRetainedPayloadPrewarmResult RetainedPayloadPrewarm,
    RadarProcessingWorkerTelemetrySummary? WorkerTelemetry)
{
    public bool IsCache => CachePath is not null;

    public bool HasWorkerTelemetry => WorkerTelemetry is not null;

    public bool HasRetainedPayloadPrewarm => RetainedPayloadPrewarm.Applied;

    public long TotalExaminedFiles => ExaminedFilesPerIteration * Iterations;

    public long TotalSkippedFiles => SkippedFilesPerIteration * Iterations;

    public long TotalPublishedFiles => PublishedFilesPerIteration * Iterations;

    public long TotalFileSizeBytes => FileSizeBytesPerIteration * Iterations;

    public long TotalCompressedRecords => CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalBatches => BatchesPerIteration * Iterations;

    public long TotalEvents => EventsPerIteration * Iterations;

    public long TotalPayloadBytes => PayloadBytesPerIteration * Iterations;

    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;

    public long ProcessingValidationFailedBatchCount => FailedValidationBatchCount;

    public long WorkerFailedBatchCount => WorkerTelemetry?.Counters.FailedBatchCount ?? 0;

    public long WorkerFailedWorkItemCount => WorkerTelemetry?.Counters.FailedWorkItemCount ?? 0;

    public double CompressedMegabytesPerSecond => MegabytesPerSecond(TotalCompressedBytes, Elapsed);

    public double DecompressedMegabytesPerSecond => MegabytesPerSecond(TotalDecompressedBytes, Elapsed);

    public double FilesPerSecond => PerSecond(TotalPublishedFiles, Elapsed);

    public double BatchesPerSecond => PerSecond(TotalBatches, Elapsed);

    public double EventsPerSecond => PerSecond(TotalEvents, Elapsed);

    public double PayloadValuesPerSecond => PerSecond(TotalPayloadValues, Elapsed);

    public double AllocatedBytesPerStreamEvent => Ratio(AllocatedBytes, TotalEvents);

    public double AllocatedBytesPerPayloadValue => Ratio(AllocatedBytes, TotalPayloadValues);

    public TimeSpan ProducerElapsed => OverlapTelemetry.ProducerActiveTime;

    public TimeSpan ConsumerElapsed => OverlapTelemetry.ConsumerActiveTime;

    public TimeSpan OverlapElapsed => OverlapTelemetry.OverlapElapsed;

    public long CurrentCombinedRetainedBatchCount =>
        QueueTelemetry.CurrentCombinedRetainedBatchCount;

    public long CurrentCombinedRetainedPayloadBytes =>
        QueueTelemetry.CurrentCombinedRetainedPayloadBytes;

    public long CombinedRetainedBatchCountHighWatermark =>
        QueueTelemetry.CombinedRetainedBatchCountHighWatermark;

    public long CombinedRetainedPayloadBytesHighWatermark =>
        QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark;

    public long ActiveRetainedBatchCountHighWatermark =>
        QueueTelemetry.ActiveRetainedBatchCountHighWatermark;

    public long ActiveRetainedPayloadBytesHighWatermark =>
        QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark;

    private static double MegabytesPerSecond(
        long bytes,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : bytes / 1_000_000d / elapsed.TotalSeconds;

    private static double PerSecond(
        long value,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : value / elapsed.TotalSeconds;

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
