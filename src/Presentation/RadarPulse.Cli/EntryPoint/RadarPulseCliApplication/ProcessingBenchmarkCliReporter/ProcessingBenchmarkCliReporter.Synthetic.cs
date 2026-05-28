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
    public static void PrintProcessingBenchmarkResult(RadarProcessingBenchmarkResult result)
    {
        Console.WriteLine("Processing benchmark: synthetic");
        Console.WriteLine("Measured contour: RadarProcessingCore over prebuilt RadarEventBatch");
        Console.WriteLine("Excluded work: decompression, Archive Two scanning, identity normalization, batch construction");
        Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
        Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
        Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
        Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
        Console.WriteLine($"Handler set: {FormatProcessingHandlerSet(result.HandlerSet)}");
        Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
        Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
        Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
        Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
        Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
        Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
        Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
        Console.WriteLine($"Active source count: {FormatNumber(result.ActiveSourceCount)}");
        Console.WriteLine($"Total batches: {FormatNumber(result.TotalBatches)}");
        Console.WriteLine($"Total stream events: {FormatNumber(result.TotalEvents)}");
        Console.WriteLine($"Total payload values: {FormatNumber(result.TotalPayloadValues)}");
        Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
        Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Batches/s: {FormatDecimal(result.BatchesPerSecond)}");
        Console.WriteLine($"Stream events/s: {FormatDecimal(result.EventsPerSecond)}");
        Console.WriteLine($"Payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
        Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
        Console.WriteLine($"Allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerEvent)}");
        Console.WriteLine($"Allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
        if (result.WorkerTelemetry is not null)
        {
            PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
        }

        if (result.AsyncValidation is not null)
        {
            Console.WriteLine($"Async validation: {FormatBoolean(result.AsyncValidation.IsValid)}");
            if (result.AsyncValidation.HasComparisonChecksums)
            {
                Console.WriteLine($"Sync comparison checksum: {FormatUnsignedNumber(result.AsyncValidation.SynchronousChecksum!.Value)}");
                Console.WriteLine($"Async comparison checksum: {FormatUnsignedNumber(result.AsyncValidation.AsyncChecksum!.Value)}");
            }
        }

        foreach (var shard in result.ShardDistributions)
        {
            Console.WriteLine($"Shard {FormatNumber(shard.ShardId)} events per iteration: {FormatNumber(shard.EventCount)}");
        }
    }

    public static void PrintProcessingWorkerTelemetry(RadarProcessingWorkerTelemetrySummary workerTelemetry)
    {
        Console.WriteLine($"Worker count: {FormatNumber(workerTelemetry.WorkerCount)}");
        Console.WriteLine($"Worker queue capacity: {FormatNumber(workerTelemetry.QueueCapacity)}");
        Console.WriteLine($"Worker dispatched batches: {FormatNumber(workerTelemetry.Counters.DispatchedBatchCount)}");
        Console.WriteLine($"Worker completed batches: {FormatNumber(workerTelemetry.Counters.CompletedBatchCount)}");
        Console.WriteLine($"Worker failed batches: {FormatNumber(workerTelemetry.Counters.FailedBatchCount)}");
        Console.WriteLine($"Worker submitted items: {FormatNumber(workerTelemetry.Counters.SubmittedWorkItemCount)}");
        Console.WriteLine($"Worker accepted items: {FormatNumber(workerTelemetry.Counters.AcceptedWorkItemCount)}");
        Console.WriteLine($"Worker completed items: {FormatNumber(workerTelemetry.Counters.CompletedWorkItemCount)}");
        Console.WriteLine($"Worker succeeded items: {FormatNumber(workerTelemetry.Counters.SucceededWorkItemCount)}");
        Console.WriteLine($"Worker failed items: {FormatNumber(workerTelemetry.Counters.FailedWorkItemCount)}");
        Console.WriteLine($"Worker dispatch ms: {FormatDecimal(workerTelemetry.Counters.TotalDispatchTime.TotalMilliseconds)}");
        Console.WriteLine($"Worker queue wait ms: {FormatDecimal(workerTelemetry.Counters.TotalQueueWaitTime.TotalMilliseconds)}");
        Console.WriteLine($"Worker execution ms: {FormatDecimal(workerTelemetry.Counters.TotalExecutionTime.TotalMilliseconds)}");
        Console.WriteLine($"Worker aggregation ms: {FormatDecimal(workerTelemetry.Counters.TotalAggregationTime.TotalMilliseconds)}");
        Console.WriteLine($"Worker barrier wait ms: {FormatDecimal(workerTelemetry.Counters.TotalBarrierWaitTime.TotalMilliseconds)}");
    }

    public static void PrintProcessingRebalanceBenchmarkResult(RadarProcessingSyntheticRebalanceBenchmarkResult result)
    {
        Console.WriteLine("Processing benchmark: rebalance-synthetic");
        Console.WriteLine("Measured contour: RadarProcessingCore plus rebalance evaluation over prebuilt synthetic RadarEventBatch values");
        Console.WriteLine("Excluded work: decompression, Archive Two scanning, identity normalization, batch construction, CLI formatting");
        Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
        Console.WriteLine($"Workload: {FormatProcessingRebalanceWorkload(result.WorkloadKind)}");
        Console.WriteLine($"Benchmark mode: {FormatProcessingRebalanceMode(result.Mode)}");
        Console.WriteLine($"Ordered active batch capacity: {FormatNumber(result.OrderedActiveBatchCapacity)}");
        Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
        Console.WriteLine($"Telemetry retention mode: {FormatProcessingRetentionMode(result.RetentionMode)}");
        PrintProcessingQuarantineLifecycle(
            result.QuarantineTtlEvaluations,
            result.QuarantineSustainedCoolingSampleCount,
            result.QuarantineMaterialPressureChangeThreshold);
        Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
        Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
        Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
        Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
        Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
        Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
        Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
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
        Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
        Console.WriteLine($"Skipped reasons: {FormatProcessingRebalanceSkippedReasons(result.SkippedReasons)}");
        Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Batches/s: {FormatDecimal(result.BatchesPerSecond)}");
        Console.WriteLine($"Stream events/s: {FormatDecimal(result.EventsPerSecond)}");
        Console.WriteLine($"Payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
        Console.WriteLine($"Rebalance evaluations/s: {FormatDecimal(result.RebalanceEvaluationsPerSecond)}");
        Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
        Console.WriteLine($"Allocation includes CLI formatting: {FormatBoolean(result.AllocationSummary.IncludesCliFormatting)}");
        Console.WriteLine($"Allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
        Console.WriteLine($"Allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
        Console.WriteLine($"Allocated bytes / rebalance evaluation: {FormatDecimal(result.AllocatedBytesPerRebalanceEvaluation)}");
        if (result.WorkerTelemetry is not null)
        {
            PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
        }

        PrintProcessingRebalanceMovePressures(result.AcceptedMovePressures);
    }

}
