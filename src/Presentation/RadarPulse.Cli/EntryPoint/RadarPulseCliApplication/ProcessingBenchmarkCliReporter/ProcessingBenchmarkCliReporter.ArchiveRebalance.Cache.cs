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
    public static void PrintProcessingArchiveRebalanceCacheBenchmarkResult(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkArchiveRebalanceOptions options)
    {
        var queueTelemetryOutput = options.QueueTelemetryOutput;
        var overlapTelemetryOutput = options.OverlapTelemetryOutput;
        Console.WriteLine("Processing benchmark: rebalance-archive cache");
        Console.WriteLine("Measured contour: Archive cache replay to RadarEventBatch plus processing rebalance callback");
        Console.WriteLine("Processing-only timing: RadarEventBatch callback inside archive publisher");
        Console.WriteLine(result.ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned
            ? "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue"
            : "Batch lifetime: leased batches are processed during the callback and are not retained");
        Console.WriteLine($"Cache: {result.CachePath}");
        if (result.Date is { } date)
        {
            Console.WriteLine($"Date: {date:yyyy-MM-dd}");
        }

        if (result.RadarId is not null)
        {
            Console.WriteLine($"Radar: {result.RadarId}");
        }

        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Archive parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine($"Provider mode: {FormatProcessingArchiveProviderMode(result.ProviderMode)}");
        Console.WriteLine($"Provider queue capacity: {FormatNumber(result.QueueCapacity)}");
        Console.WriteLine($"Provider overlap mode: {FormatProcessingProviderOverlapMode(result.ProviderOverlapMode)}");
        Console.WriteLine($"Provider overlap consumer delay ms: {FormatDecimal(result.OverlapConsumerDelay.TotalMilliseconds)}");
        Console.WriteLine($"Retention strategy: {FormatProcessingRetentionStrategy(result.RetentionStrategy)}");
        Console.WriteLine($"Provider queue retained byte capacity: {FormatOptionalNumber(result.QueueRetainedPayloadBytes)}");
        PrintProcessingRetainedPayloadPrewarm(result.RetainedPayloadPrewarm);
        var providerOverlapEvidenceContour =
            FormatProviderOverlapEvidenceContourForCacheBenchmark(result, queueTelemetryOutput, overlapTelemetryOutput);
        var isDefaultCandidateContour =
            IsDefaultCandidateCacheBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput);
        PrintProcessingArchiveRebalanceProviderSelection(
            result.ProviderMode,
            result.ProviderOverlapMode,
            result.ExecutionMode,
            options.EffectiveOptionProvenance,
            isDefaultCandidateContour,
            options.IsRolloutDefaultExpandedContour,
            options.IsExplicitBlockingBorrowedFallback);
        Console.WriteLine($"Default-candidate contour: {FormatBoolean(isDefaultCandidateContour)}");
        Console.WriteLine($"Provider overlap evidence contour: {providerOverlapEvidenceContour}");
        Console.WriteLine($"Provider overlap evidence scope: {FormatProviderOverlapEvidenceScope(providerOverlapEvidenceContour)}");
        Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
        Console.WriteLine($"Benchmark mode: {FormatProcessingRebalanceMode(result.Mode)}");
        Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
        Console.WriteLine($"Telemetry retention mode: {FormatProcessingRetentionMode(result.RetentionMode)}");
        PrintProcessingQuarantineLifecycle(
            result.QuarantineTtlEvaluations,
            result.QuarantineSustainedCoolingSampleCount,
            result.QuarantineMaterialPressureChangeThreshold);
        Console.WriteLine($"Max retained decisions: {FormatNumber(result.MaxRetainedDecisions)}");
        Console.WriteLine($"Max retained lifecycle transitions: {FormatNumber(result.MaxRetainedLifecycleTransitions)}");
        Console.WriteLine($"Max retained accepted moves: {FormatNumber(result.MaxRetainedAcceptedMoves)}");
        Console.WriteLine($"Max retained validation failures: {FormatNumber(result.MaxRetainedValidationFailures)}");
        PrintProcessingPressureSkew(result.PressureSkew);
        Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
        Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
        Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
        Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
        Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
        Console.WriteLine($"Examined files per iteration: {FormatNumber(result.ExaminedFilesPerIteration)}");
        Console.WriteLine($"Skipped files per iteration: {FormatNumber(result.SkippedFilesPerIteration)}");
        Console.WriteLine($"Published files per iteration: {FormatNumber(result.PublishedFilesPerIteration)}");
        Console.WriteLine($"File size bytes per iteration: {FormatNumber(result.FileSizeBytesPerIteration)}");
        Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
        Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
        Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
        Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
        Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
        Console.WriteLine($"Payload bytes per iteration: {FormatNumber(result.PayloadBytesPerIteration)}");
        Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
        Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
        Console.WriteLine($"Topology versions per iteration: {FormatNumber(result.TopologyVersionCount)}");
        Console.WriteLine($"Rebalance evaluations: {FormatNumber(result.RebalanceEvaluationCount)}");
        Console.WriteLine($"Accepted moves: {FormatNumber(result.AcceptedMoveCount)}");
        Console.WriteLine($"Skipped decisions: {FormatNumber(result.SkippedDecisionCount)}");
        Console.WriteLine($"Direct hot relief moves: {FormatNumber(result.DirectHotReliefCount)}");
        Console.WriteLine($"Cold evacuation moves: {FormatNumber(result.ColdEvacuationCount)}");
        Console.WriteLine($"Failed migrations: {FormatNumber(result.FailedMigrationCount)}");
        Console.WriteLine($"Validation: {(result.ValidationSucceeded ? "succeeded" : "failed")}");
        Console.WriteLine($"Processing completeness: {(result.ProcessingSucceeded ? "succeeded" : "failed")}");
        Console.WriteLine($"Processing validation failed batches: {FormatNumber(result.ProcessingValidationFailedBatchCount)}");
        Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
        Console.WriteLine($"Skipped reasons: {FormatProcessingRebalanceSkippedReasons(result.SkippedReasons)}");
        Console.WriteLine($"Skipped reason counters: {FormatProcessingRebalanceSkippedReasonCounters(result.SkippedReasonCounters)}");
        PrintProcessingRebalanceRetentionStats(result.RetentionStats);
        Console.WriteLine($"End-to-end elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Processing callback elapsed ms: {FormatDecimal(result.ProcessingElapsed.TotalMilliseconds)}");
        Console.WriteLine($"Replay and batch construction elapsed ms: {FormatDecimal(result.ReplayAndBatchConstructionElapsed.TotalMilliseconds)}");
        PrintProcessingProviderQueueTelemetryForArchiveCache(result, queueTelemetryOutput);
        PrintProcessingProviderRetentionTelemetryForArchiveCache(result);
        PrintProcessingProviderOverlapTelemetryForArchiveCache(result, overlapTelemetryOutput);
        Console.WriteLine($"Compressed MB/s: {FormatDecimal(result.CompressedMegabytesPerSecond)}");
        Console.WriteLine($"Decompressed MB/s: {FormatDecimal(result.DecompressedMegabytesPerSecond)}");
        Console.WriteLine($"Files/s: {FormatDecimal(result.FilesPerSecond)}");
        Console.WriteLine($"End-to-end stream events/s: {FormatDecimal(result.EventsPerSecond)}");
        Console.WriteLine($"End-to-end payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
        Console.WriteLine($"Processing stream events/s: {FormatDecimal(result.ProcessingEventsPerSecond)}");
        Console.WriteLine($"Processing payload values/s: {FormatDecimal(result.ProcessingPayloadValuesPerSecond)}");
        Console.WriteLine($"Rebalance evaluations/s: {FormatDecimal(result.RebalanceEvaluationsPerSecond)}");
        Console.WriteLine($"End-to-end allocated bytes: {FormatNumber(result.AllocatedBytes)}");
        Console.WriteLine($"Processing callback allocated bytes: {FormatNumber(result.ProcessingCallbackAllocatedBytes)}");
        Console.WriteLine($"Replay and batch construction allocated bytes: {FormatNumber(result.ReplayAndBatchConstructionAllocatedBytes)}");
        PrintProcessingArchiveRebalanceAllocationAttribution(
            result.AllocationSummary,
            result.TotalPayloadValues);
        Console.WriteLine($"End-to-end allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
        Console.WriteLine($"End-to-end allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
        Console.WriteLine($"Processing callback allocated bytes / payload value: {FormatDecimal(result.ProcessingCallbackAllocatedBytesPerPayloadValue)}");
        Console.WriteLine($"Processing callback allocated bytes / rebalance evaluation: {FormatDecimal(result.ProcessingCallbackAllocatedBytesPerRebalanceEvaluation)}");
        Console.WriteLine($"Replay and batch construction allocated bytes / payload value: {FormatDecimal(result.ReplayAndBatchConstructionAllocatedBytesPerPayloadValue)}");
        if (result.WorkerTelemetry is not null)
        {
            PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
        }

        PrintProcessingRebalanceMovePressures(result.AcceptedMovePressures);
    }
}
