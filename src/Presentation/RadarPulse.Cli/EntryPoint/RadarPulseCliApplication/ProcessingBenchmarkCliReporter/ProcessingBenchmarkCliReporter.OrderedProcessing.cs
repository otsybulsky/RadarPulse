using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;
using static CliFormat;

internal static partial class ProcessingBenchmarkCliReporter
{
    public static void PrintProcessingArchiveOrderedProcessingBenchmarkResult(
        RadarProcessingArchiveOrderedProcessingBenchmarkResult result,
        ProcessingBenchmarkOrderedArchiveProcessingOptions options)
    {
        Console.WriteLine(result.IsCache
            ? "Processing benchmark: ordered-archive-processing cache"
            : "Processing benchmark: ordered-archive-processing");
        Console.WriteLine("Measured contour: Archive replay to RadarEventBatch through runtime/archive MVP processing path");
        Console.WriteLine(result.HandlerSet == RadarProcessingBenchmarkHandlerSet.None
            ? "Processing path: RunProcessingAsync ordered active-batch drain"
            : result.ActiveBatchCapacity == 1
                ? "Processing path: RunMvpProcessingAsync sequential handler-aware drain"
                : "Processing path: RunMvpProcessingAsync handler delta/merge");
        Console.WriteLine("Processing-only timing: provider/consumer overlap result around ordered processing drain");
        Console.WriteLine("Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue");
        if (result.IsCache)
        {
            Console.WriteLine($"Cache: {result.CachePath}");
            if (result.Date is { } date)
            {
                Console.WriteLine($"Date: {date:yyyy-MM-dd}");
            }

            if (result.RadarId is not null)
            {
                Console.WriteLine($"Radar: {result.RadarId}");
            }
        }
        else
        {
            Console.WriteLine($"File: {result.FilePath}");
        }

        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Handler set: {FormatProcessingHandlerSet(result.HandlerSet)}");
        Console.WriteLine($"Archive parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine($"Provider mode: {FormatProcessingArchiveProviderMode(RadarProcessingArchiveProviderMode.QueuedOwned)}");
        Console.WriteLine($"Provider queue capacity: {FormatNumber(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity)}");
        Console.WriteLine($"Provider overlap mode: {FormatProcessingProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)}");
        Console.WriteLine($"Retention strategy: {FormatProcessingRetentionStrategy(RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy)}");
        Console.WriteLine($"Provider queue retained byte capacity: {FormatNumber(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes)}");
        PrintProcessingRetainedPayloadPrewarm(result.RetainedPayloadPrewarm);
        Console.WriteLine("Provider mode source: runtime-archive-baseline");
        Console.WriteLine("Provider overlap source: runtime-archive-baseline");
        Console.WriteLine("Retention strategy source: runtime-archive-baseline");
        Console.WriteLine("Provider queue capacity source: runtime-archive-baseline");
        Console.WriteLine("Provider queue retained byte capacity source: runtime-archive-baseline");
        Console.WriteLine("Execution mode source: runtime-archive-baseline");
        Console.WriteLine("Worker count source: runtime-archive-baseline");
        Console.WriteLine("Ordered active batch capacity source: explicit-or-baseline");
        Console.WriteLine($"Execution mode: {FormatProcessingMode(RadarProcessingRuntimeArchiveBaseline.ExecutionMode)}");
        Console.WriteLine($"Ordered active batch capacity: {FormatNumber(result.ActiveBatchCapacity)}");
        Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
        Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
        Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
        Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
        Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
        if (result.IsCache)
        {
            Console.WriteLine($"Examined files per iteration: {FormatNumber(result.ExaminedFilesPerIteration)}");
            Console.WriteLine($"Skipped files per iteration: {FormatNumber(result.SkippedFilesPerIteration)}");
            Console.WriteLine($"Published files per iteration: {FormatNumber(result.PublishedFilesPerIteration)}");
        }

        Console.WriteLine($"File size bytes per iteration: {FormatNumber(result.FileSizeBytesPerIteration)}");
        Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
        Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
        Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
        Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
        Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
        Console.WriteLine($"Payload bytes per iteration: {FormatNumber(result.PayloadBytesPerIteration)}");
        Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
        Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
        Console.WriteLine($"Run status: {FormatProcessingArchiveQueuedOverlapStatus(result.Status)}");
        Console.WriteLine($"Consumer status: {FormatProcessingQueuedSessionStatus(result.ConsumerStatus)}");
        Console.WriteLine($"Processing completeness: {(result.ProcessingSucceeded ? "succeeded" : "failed")}");
        Console.WriteLine($"Processing succeeded batches: {FormatNumber(result.SucceededBatchCount)}");
        Console.WriteLine($"Processing failed batches: {FormatNumber(result.FailedProcessingBatchCount)}");
        Console.WriteLine($"Processing validation failed batches: {FormatNumber(result.ProcessingValidationFailedBatchCount)}");
        Console.WriteLine($"Processing canceled batches: {FormatNumber(result.CanceledBatchCount)}");
        Console.WriteLine($"Processing skipped after fault batches: {FormatNumber(result.SkippedAfterFaultBatchCount)}");
        Console.WriteLine($"Final processed batches: {FormatNumber(result.FinalProcessedBatchCount)}");
        Console.WriteLine($"Final processed stream events: {FormatNumber(result.FinalProcessedStreamEventCount)}");
        Console.WriteLine($"Final processed payload values: {FormatNumber(result.FinalProcessedPayloadValueCount)}");
        Console.WriteLine($"Final raw value checksum: {FormatNumber(result.FinalRawValueChecksum)}");
        Console.WriteLine($"Final processing checksum: {FormatUnsignedNumber(result.FinalProcessingChecksum)}");
        Console.WriteLine($"End-to-end elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Producer active ms: {FormatDecimal(result.ProducerElapsed.TotalMilliseconds)}");
        Console.WriteLine($"Consumer active ms: {FormatDecimal(result.ConsumerElapsed.TotalMilliseconds)}");
        Console.WriteLine($"Producer-consumer overlap ms: {FormatDecimal(result.OverlapElapsed.TotalMilliseconds)}");
        if (options.QueueTelemetryOutput != ProcessingBenchmarkProviderQueueTelemetryOutput.None)
        {
            PrintProcessingProviderQueueTelemetrySummary(
                result.QueueTelemetry,
                Ratio(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, result.TotalPayloadValues),
                options.QueueTelemetryOutput);
        }

        PrintProcessingProviderRetentionTelemetrySummary(result.OverlapTelemetry.RetentionTelemetry);
        if (options.OverlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
        {
            PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, options.OverlapTelemetryOutput);
        }

        Console.WriteLine($"Compressed MB/s: {FormatDecimal(result.CompressedMegabytesPerSecond)}");
        Console.WriteLine($"Decompressed MB/s: {FormatDecimal(result.DecompressedMegabytesPerSecond)}");
        if (result.IsCache)
        {
            Console.WriteLine($"Files/s: {FormatDecimal(result.FilesPerSecond)}");
        }

        Console.WriteLine($"End-to-end stream events/s: {FormatDecimal(result.EventsPerSecond)}");
        Console.WriteLine($"End-to-end payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
        Console.WriteLine($"End-to-end allocated bytes: {FormatNumber(result.AllocatedBytes)}");
        Console.WriteLine("Allocation measured counter scope: global");
        Console.WriteLine("Allocation excludes startup retained payload prewarm: yes");
        Console.WriteLine($"End-to-end allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
        Console.WriteLine($"End-to-end allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
        if (result.WorkerTelemetry is not null)
        {
            PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
        }
    }

}
