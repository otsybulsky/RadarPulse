using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private static RadarProcessingArchiveOrderedProcessingBenchmarkResult CreateResult(
        OrderedProcessingIterationTelemetry measurement,
        string? filePath,
        string? cachePath,
        DateOnly? date,
        string? radarId,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        int sourceCount,
        int partitionCount,
        int shardCount,
        int activeBatchCapacity,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        TimeSpan elapsed,
        long allocatedBytes) =>
        new(
            filePath,
            cachePath,
            date,
            radarId,
            measurement.Decompressor,
            handlerSet,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceCount,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            measurement.ExaminedFiles,
            measurement.SkippedFiles,
            measurement.PublishedFiles,
            measurement.FileSizeBytes,
            measurement.CompressedRecordCount,
            measurement.CompressedBytes,
            measurement.DecompressedBytes,
            measurement.BatchCount,
            measurement.EventCount,
            measurement.PayloadBytes,
            measurement.PayloadValueCount,
            measurement.RawValueChecksum,
            measurement.Status,
            measurement.ConsumerStatus,
            measurement.SucceededBatchCount,
            measurement.FailedProcessingBatchCount,
            measurement.FailedValidationBatchCount,
            measurement.CanceledBatchCount,
            measurement.SkippedAfterFaultBatchCount,
            measurement.FinalProcessedBatchCount,
            measurement.FinalProcessedStreamEventCount,
            measurement.FinalProcessedPayloadValueCount,
            measurement.FinalRawValueChecksum,
            measurement.FinalProcessingChecksum,
            measurement.ProcessingSucceeded,
            elapsed,
            allocatedBytes,
            measurement.QueueTelemetry,
            measurement.OverlapTelemetry,
            measurement.RetainedPayloadPrewarm,
            measurement.WorkerTelemetry);
}
