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

internal static class ProcessingBenchmarkCliReporter
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

    public static void PrintProcessingArchiveRebalanceBenchmarkResult(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkArchiveRebalanceOptions options)
    {
        var queueTelemetryOutput = options.QueueTelemetryOutput;
        var overlapTelemetryOutput = options.OverlapTelemetryOutput;
        Console.WriteLine("Processing benchmark: rebalance-archive");
        Console.WriteLine("Measured contour: Archive replay to RadarEventBatch plus processing rebalance callback");
        Console.WriteLine("Processing-only timing: RadarEventBatch callback inside archive publisher");
        Console.WriteLine(result.ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned
            ? "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue"
            : "Batch lifetime: leased batches are processed during the callback and are not retained");
        Console.WriteLine($"File: {result.FilePath}");
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
            FormatProviderOverlapEvidenceContourForFileBenchmark(result, queueTelemetryOutput, overlapTelemetryOutput);
        var isDefaultCandidateContour =
            IsDefaultCandidateFileBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput);
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
        PrintProcessingProviderQueueTelemetryForArchiveFile(result, queueTelemetryOutput);
        PrintProcessingProviderRetentionTelemetryForArchiveFile(result);
        PrintProcessingProviderOverlapTelemetryForArchiveFile(result, overlapTelemetryOutput);
        Console.WriteLine($"Compressed MB/s: {FormatDecimal(result.CompressedMegabytesPerSecond)}");
        Console.WriteLine($"Decompressed MB/s: {FormatDecimal(result.DecompressedMegabytesPerSecond)}");
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

    public static void PrintProcessingArchiveRebalanceProviderSelection(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingExecutionMode executionMode,
        ProcessingBenchmarkArchiveRebalanceOptionProvenance provenance,
        bool isDefaultCandidateContour,
        bool isRolloutDefaultExpandedContour,
        bool isExplicitBlockingBorrowedFallback)
    {
        var isQueuedOwned = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned;
        var hasProducerConsumerOverlap =
            isQueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer;
        var isAsyncExecution = executionMode == RadarProcessingExecutionMode.AsyncShardTransport;

        Console.WriteLine($"Provider mode source: {FormatProcessingBenchmarkOptionValueSource(provenance.ProviderMode)}");
        Console.WriteLine($"Provider overlap source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.ProviderOverlapMode, isQueuedOwned)}");
        Console.WriteLine($"Retention strategy source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.RetentionStrategy, isQueuedOwned)}");
        Console.WriteLine($"Provider queue capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueCapacity, isQueuedOwned)}");
        Console.WriteLine($"Worker queue capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueCapacity, isAsyncExecution)}");
        Console.WriteLine($"Provider queue retained byte capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueRetainedPayloadBytes, isQueuedOwned)}");
        Console.WriteLine($"Queue telemetry source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueTelemetry, isQueuedOwned)}");
        Console.WriteLine($"Provider overlap telemetry source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.OverlapTelemetry, hasProducerConsumerOverlap)}");
        Console.WriteLine($"Provider overlap consumer delay source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.OverlapConsumerDelay, hasProducerConsumerOverlap)}");
        Console.WriteLine($"Execution mode source: {FormatProcessingBenchmarkOptionValueSource(provenance.ExecutionMode)}");
        Console.WriteLine($"Worker count source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.WorkerCount, isAsyncExecution)}");
        Console.WriteLine($"Provider default rollout contour: {FormatBoolean(isDefaultCandidateContour)}");
        Console.WriteLine($"Provider rollout default expansion: {FormatBoolean(isRolloutDefaultExpandedContour)}");
        Console.WriteLine($"Provider fallback contour: {FormatBoolean(isExplicitBlockingBorrowedFallback)}");
    }

    public static void PrintProcessingArchiveRebalanceAllocationAttribution(
        RadarProcessingRebalanceAllocationSummary allocation,
        long payloadValueCount)
    {
        Console.WriteLine("Allocation attribution: summary");
        Console.WriteLine("Allocation measured counter scope: global");
        Console.WriteLine("Allocation processing callback counter scope: global");
        Console.WriteLine($"Allocation measured bytes: {FormatNumber(allocation.MeasuredAllocatedBytes)}");
        Console.WriteLine($"Allocation processing callback bytes: {FormatNumber(allocation.ProcessingCallbackAllocatedBytes)}");
        Console.WriteLine($"Allocation replay and batch construction bytes: {FormatNumber(allocation.ReplayAndBatchConstructionAllocatedBytes)}");
        Console.WriteLine($"Allocation owned snapshot bytes: {FormatNumber(allocation.OwnedSnapshotAllocatedBytes)}");
        Console.WriteLine($"Allocation processing callback non-owned snapshot bytes: {FormatNumber(allocation.ProcessingCallbackNonOwnedSnapshotAllocatedBytes)}");
        Console.WriteLine($"Allocation includes archive replay and batch construction: {FormatBoolean(allocation.IncludesArchiveReplayAndBatchConstruction)}");
        Console.WriteLine($"Allocation includes CLI formatting: {FormatBoolean(allocation.IncludesCliFormatting)}");
        Console.WriteLine($"Allocation owned snapshot bytes / payload value: {FormatDecimal(allocation.OwnedSnapshotAllocatedBytesPerPayloadValue(payloadValueCount))}");
        Console.WriteLine($"Allocation processing callback non-owned snapshot bytes / payload value: {FormatDecimal(allocation.ProcessingCallbackNonOwnedSnapshotAllocatedBytesPerPayloadValue(payloadValueCount))}");
    }

    public static void PrintProcessingRetainedPayloadPrewarm(
        RadarProcessingRetainedPayloadPrewarmResult prewarm)
    {
        Console.WriteLine($"Retained payload prewarm: {FormatBoolean(prewarm.Applied)}");
        if (!prewarm.Applied)
        {
            return;
        }

        Console.WriteLine($"Retained payload prewarm event count: {FormatNumber(prewarm.EventCount)}");
        Console.WriteLine($"Retained payload prewarm payload bytes: {FormatNumber(prewarm.PayloadBytes)}");
        Console.WriteLine($"Retained payload prewarm batch count: {FormatNumber(prewarm.RetainedBatchCount)}");
        Console.WriteLine($"Retained payload prewarm elapsed ms: {FormatDecimal(prewarm.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Retained payload prewarm allocated bytes: {FormatNumber(prewarm.AllocatedBytes)}");
        Console.WriteLine($"Retained payload prewarm retained bytes: {FormatNumber(prewarm.RetainedBytes)}");
    }

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

    public static void PrintProcessingProviderQueueTelemetryForArchiveFile(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
    {
        if (!result.HasQueueTelemetry ||
            queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderQueueTelemetrySummary(
            result.QueueTelemetry,
            result.OwnedSnapshotAllocatedBytesPerPayloadValue,
            queueTelemetryOutput);
    }

    public static void PrintProcessingProviderQueueTelemetryForArchiveCache(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
    {
        if (!result.HasQueueTelemetry ||
            queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderQueueTelemetrySummary(
            result.QueueTelemetry,
            result.OwnedSnapshotAllocatedBytesPerPayloadValue,
            queueTelemetryOutput);
    }

    public static void PrintProcessingProviderRetentionTelemetryForArchiveFile(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        if (!result.HasRetentionTelemetry)
        {
            return;
        }

        PrintProcessingProviderRetentionTelemetrySummary(result.RetentionTelemetry);
    }

    public static void PrintProcessingProviderRetentionTelemetryForArchiveCache(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        if (!result.HasRetentionTelemetry)
        {
            return;
        }

        PrintProcessingProviderRetentionTelemetrySummary(result.RetentionTelemetry);
    }

    public static void PrintProcessingProviderRetentionTelemetrySummary(
        RadarProcessingRetainedPayloadTelemetrySummary telemetry)
    {
        Console.WriteLine("Retained payload telemetry: summary");
        Console.WriteLine("Retained payload allocation counter scope: current-thread");
        Console.WriteLine($"Retained payload strategy: {FormatProcessingRetentionStrategy(telemetry.Strategy)}");
        Console.WriteLine($"Retained payload attempts: {FormatNumber(telemetry.RetentionAttemptCount)}");
        Console.WriteLine($"Retained payload batches: {FormatNumber(telemetry.RetainedBatchCount)}");
        Console.WriteLine($"Retained payload events: {FormatNumber(telemetry.RetainedEventCount)}");
        Console.WriteLine($"Retained payload bytes: {FormatNumber(telemetry.RetainedPayloadBytes)}");
        Console.WriteLine($"Retained payload values: {FormatNumber(telemetry.RetainedPayloadValueCount)}");
        Console.WriteLine($"Retained payload allocated bytes: {FormatNumber(telemetry.AllocatedBytes)}");
        Console.WriteLine($"Retained payload elapsed ms: {FormatDecimal(telemetry.TotalRetentionTime.TotalMilliseconds)}");
        Console.WriteLine($"Retained payload transfers: {FormatNumber(telemetry.TransferCount)}");
        Console.WriteLine($"Retained payload pool rents: {FormatNumber(telemetry.PoolRentCount)}");
        Console.WriteLine($"Retained payload pool returns: {FormatNumber(telemetry.PoolReturnCount)}");
        Console.WriteLine($"Retained payload pool misses: {FormatNumber(telemetry.PoolMissCount)}");
        Console.WriteLine($"Retained event array pool rents: {FormatNumber(telemetry.EventPoolRentCount)}");
        Console.WriteLine($"Retained event array pool returns: {FormatNumber(telemetry.EventPoolReturnCount)}");
        Console.WriteLine($"Retained event array pool misses: {FormatNumber(telemetry.EventPoolMissCount)}");
        Console.WriteLine($"Retained byte array pool rents: {FormatNumber(telemetry.PayloadPoolRentCount)}");
        Console.WriteLine($"Retained byte array pool returns: {FormatNumber(telemetry.PayloadPoolReturnCount)}");
        Console.WriteLine($"Retained byte array pool misses: {FormatNumber(telemetry.PayloadPoolMissCount)}");
        Console.WriteLine($"Retained payload unsupported strategy attempts: {FormatNumber(telemetry.RetentionUnsupportedStrategyCount)}");
        Console.WriteLine($"Retained payload failed copies: {FormatNumber(telemetry.RetentionFailedCopyCount)}");
        Console.WriteLine($"Retained payload canceled retentions: {FormatNumber(telemetry.RetentionCanceledCount)}");
        Console.WriteLine($"Retained payload invalid inputs: {FormatNumber(telemetry.RetentionInvalidInputCount)}");
        Console.WriteLine($"Retained payload release attempts: {FormatNumber(telemetry.ReleaseAttemptCount)}");
        Console.WriteLine($"Retained payload released batches: {FormatNumber(telemetry.ReleasedBatchCount)}");
        Console.WriteLine($"Retained payload already released batches: {FormatNumber(telemetry.AlreadyReleasedBatchCount)}");
        Console.WriteLine($"Retained payload release-not-required batches: {FormatNumber(telemetry.ReleaseNotRequiredCount)}");
        Console.WriteLine($"Retained payload failed releases: {FormatNumber(telemetry.ReleaseFailedCount)}");
        Console.WriteLine($"Retained payload release elapsed ms: {FormatDecimal(telemetry.TotalReleaseTime.TotalMilliseconds)}");
    }

    public static void PrintProcessingProviderOverlapTelemetryForArchiveFile(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
    {
        if (!result.HasOverlapTelemetry ||
            overlapTelemetryOutput == ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, overlapTelemetryOutput);
    }

    public static void PrintProcessingProviderOverlapTelemetryForArchiveCache(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
    {
        if (!result.HasOverlapTelemetry ||
            overlapTelemetryOutput == ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, overlapTelemetryOutput);
    }

    public static void PrintProcessingProviderOverlapTelemetrySummary(
        RadarProcessingArchiveOverlapTelemetrySummary telemetry,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
    {
        Console.WriteLine($"Provider overlap telemetry: {FormatProviderOverlapTelemetryOutput(overlapTelemetryOutput)}");
        Console.WriteLine($"Provider overlap retained payload strategy: {FormatProcessingRetentionStrategy(telemetry.RetentionStrategy)}");
        Console.WriteLine($"Provider overlap elapsed ms: {FormatDecimal(telemetry.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap producer active ms: {FormatDecimal(telemetry.ProducerActiveTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap consumer active ms: {FormatDecimal(telemetry.ConsumerActiveTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap shared active ms: {FormatDecimal(telemetry.OverlapElapsed.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap has producer-consumer overlap: {FormatBoolean(telemetry.HasProducerConsumerOverlap)}");
        Console.WriteLine($"Provider overlap has queued-ahead overlap: {FormatBoolean(telemetry.HasQueuedAheadOverlap)}");
        Console.WriteLine($"Provider overlap queue depth high watermark: {FormatNumber(telemetry.QueueDepthHighWatermark)}");
        Console.WriteLine($"Provider overlap retained payload bytes high watermark: {FormatNumber(telemetry.RetainedPayloadBytesHighWatermark)}");
        PrintProcessingRetainedResourcePressureSummary("Provider overlap", telemetry.RetainedResourcePressure);
        Console.WriteLine($"Provider overlap provider blocked ms: {FormatDecimal(telemetry.ProviderBlockedTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap consumer idle ms: {FormatDecimal(telemetry.ConsumerIdleTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap provider-to-processing latency ms: {FormatDecimal(telemetry.TotalProviderToProcessingLatency.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap retained batches: {FormatNumber(telemetry.RetainedBatchCount)}");
        Console.WriteLine($"Provider overlap retained events: {FormatNumber(telemetry.RetainedEventCount)}");
        Console.WriteLine($"Provider overlap retained payload bytes: {FormatNumber(telemetry.RetainedPayloadBytes)}");
        Console.WriteLine($"Provider overlap retained payload values: {FormatNumber(telemetry.RetainedPayloadValueCount)}");
        Console.WriteLine($"Provider overlap retention allocated bytes: {FormatNumber(telemetry.RetentionAllocatedBytes)}");
        Console.WriteLine("Provider overlap measured allocation counter scope: global");
        Console.WriteLine($"Provider overlap measured allocated bytes: {FormatNumber(telemetry.MeasuredAllocatedBytes)}");
        Console.WriteLine($"Provider overlap unattributed allocated bytes: {FormatNumber(telemetry.UnattributedAllocatedBytes)}");
        Console.WriteLine($"Provider overlap release attempts: {FormatNumber(telemetry.ReleaseAttemptCount)}");
        Console.WriteLine($"Provider overlap released batches: {FormatNumber(telemetry.ReleasedBatchCount)}");
        Console.WriteLine($"Provider overlap release-not-required batches: {FormatNumber(telemetry.ReleaseNotRequiredCount)}");
        Console.WriteLine($"Provider overlap failed releases: {FormatNumber(telemetry.ReleaseFailedCount)}");
    }

    public static bool IsDefaultCandidateFileBenchmarkContour(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
            result.ProviderMode,
            result.QueueCapacity,
            result.ProviderOverlapMode,
            result.RetentionStrategy,
            result.QueueRetainedPayloadBytes,
            result.OverlapConsumerDelay,
            queueTelemetryOutput,
            overlapTelemetryOutput,
            result.ExecutionMode);

    public static bool IsDefaultCandidateCacheBenchmarkContour(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
            result.ProviderMode,
            result.QueueCapacity,
            result.ProviderOverlapMode,
            result.RetentionStrategy,
            result.QueueRetainedPayloadBytes,
            result.OverlapConsumerDelay,
            queueTelemetryOutput,
            overlapTelemetryOutput,
            result.ExecutionMode);

    public static string FormatProviderOverlapEvidenceContourForFileBenchmark(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        FormatProviderOverlapEvidenceContourCore(
            result.ProviderMode,
            result.ProviderOverlapMode,
            result.OverlapConsumerDelay,
            IsDefaultCandidateFileBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput));

    public static string FormatProviderOverlapEvidenceContourForCacheBenchmark(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        FormatProviderOverlapEvidenceContourCore(
            result.ProviderMode,
            result.ProviderOverlapMode,
            result.OverlapConsumerDelay,
            IsDefaultCandidateCacheBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput));

    public static string FormatProviderOverlapEvidenceContourCore(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        TimeSpan overlapConsumerDelay,
        bool isDefaultCandidateContour) =>
        ProcessingBenchmarkArchiveRebalanceOptions.FormatProviderOverlapEvidenceContour(
            providerMode,
            providerOverlapMode,
            overlapConsumerDelay,
            isDefaultCandidateContour);

    public static string FormatProviderOverlapEvidenceScope(string providerOverlapEvidenceContour) =>
        ProcessingBenchmarkArchiveRebalanceOptions.FormatProviderOverlapEvidenceScope(providerOverlapEvidenceContour);

    public static void PrintProcessingProviderQueueTelemetrySummary(
        RadarProcessingProviderQueueTelemetrySummary telemetry,
        double ownedSnapshotAllocatedBytesPerPayloadValue,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
    {
        Console.WriteLine($"Provider queue telemetry: {FormatProviderQueueTelemetryOutput(queueTelemetryOutput)}");
        Console.WriteLine($"Provider queue owned snapshots: {FormatNumber(telemetry.OwnedSnapshotCount)}");
        Console.WriteLine($"Provider queue owned snapshot events: {FormatNumber(telemetry.OwnedSnapshotEventCount)}");
        Console.WriteLine($"Provider queue owned snapshot payload bytes: {FormatNumber(telemetry.OwnedSnapshotPayloadBytes)}");
        Console.WriteLine($"Provider queue owned snapshot payload values: {FormatNumber(telemetry.OwnedSnapshotPayloadValueCount)}");
        Console.WriteLine($"Provider queue owned snapshot elapsed ms: {FormatDecimal(telemetry.TotalOwnedSnapshotTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue owned snapshot allocated bytes: {FormatNumber(telemetry.OwnedSnapshotAllocatedBytes)}");
        Console.WriteLine($"Provider queue owned snapshot allocated bytes / payload value: {FormatDecimal(ownedSnapshotAllocatedBytesPerPayloadValue)}");
        Console.WriteLine($"Provider queue enqueue attempts: {FormatNumber(telemetry.EnqueueAttemptCount)}");
        Console.WriteLine($"Provider queue enqueued batches: {FormatNumber(telemetry.EnqueuedBatchCount)}");
        Console.WriteLine($"Provider queue full batches: {FormatNumber(telemetry.EnqueueFullCount)}");
        Console.WriteLine($"Provider queue timed out batches: {FormatNumber(telemetry.EnqueueTimedOutCount)}");
        Console.WriteLine($"Provider queue canceled enqueue batches: {FormatNumber(telemetry.EnqueueCanceledCount)}");
        Console.WriteLine($"Provider queue closed enqueue batches: {FormatNumber(telemetry.EnqueueClosedCount)}");
        Console.WriteLine($"Provider queue faulted enqueue batches: {FormatNumber(telemetry.EnqueueFaultedCount)}");
        Console.WriteLine($"Provider queue enqueue wait ms: {FormatDecimal(telemetry.TotalEnqueueWaitTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue dequeued batches: {FormatNumber(telemetry.DequeuedBatchCount)}");
        Console.WriteLine($"Provider queue completed batches: {FormatNumber(telemetry.CompletedBatchCount)}");
        Console.WriteLine($"Provider queue failed batches: {FormatNumber(telemetry.FailedBatchCount)}");
        Console.WriteLine($"Provider queue canceled batches: {FormatNumber(telemetry.CanceledBatchCount)}");
        Console.WriteLine($"Provider queue skipped after fault batches: {FormatNumber(telemetry.SkippedAfterFaultCount)}");
        Console.WriteLine($"Provider queue drain ms: {FormatDecimal(telemetry.TotalDrainTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue dequeue wait ms: {FormatDecimal(telemetry.TotalDequeueWaitTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue depth high watermark: {FormatNumber(telemetry.QueueDepthHighWatermark)}");
        Console.WriteLine($"Provider queue payload bytes high watermark: {FormatNumber(telemetry.QueuedPayloadBytesHighWatermark)}");
        Console.WriteLine($"Provider queue retained payload bytes high watermark: {FormatNumber(telemetry.RetainedPayloadBytesHighWatermark)}");
        PrintProcessingRetainedResourcePressureSummary("Provider queue", telemetry.RetainedResourcePressure);
        Console.WriteLine($"Provider-to-processing latency ms: {FormatDecimal(telemetry.TotalProviderToProcessingLatency.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue retained recent details: {FormatNumber(telemetry.RetainedRecentDetailCount)}");
        Console.WriteLine($"Provider queue dropped recent details: {FormatNumber(telemetry.DroppedRecentDetailCount)}");

        if (queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.Recent)
        {
            PrintProcessingProviderQueueRecentDetails(telemetry.RecentDetails);
        }
    }

    public static void PrintProcessingProviderQueueRecentDetails(
        IReadOnlyList<RadarProcessingProviderQueueRecentDetail> recentDetails)
    {
        if (recentDetails.Count == 0)
        {
            Console.WriteLine("Provider queue recent details: (none)");
            return;
        }

        Console.WriteLine("Provider queue recent details:");
        for (var i = 0; i < recentDetails.Count; i++)
        {
            var detail = recentDetails[i];
            Console.WriteLine(
                $"  {FormatNumber(i + 1)}. {FormatProcessingProviderQueueRecentDetailKind(detail.Kind)} " +
                $"sequence {FormatProcessingProviderQueueSequence(detail.Sequence)} " +
                $"enqueue {FormatProcessingProviderQueueEnqueueStatus(detail.EnqueueStatus)} " +
                $"processing {FormatProcessingProviderQueueProcessingStatus(detail.ProcessingStatus)} " +
                $"events {FormatNumber(detail.StreamEventCount)} payload bytes {FormatNumber(detail.PayloadBytes)} " +
                $"payload values {FormatNumber(detail.PayloadValueCount)} queue depth {FormatNumber(detail.QueueDepth)}");
        }
    }

    public static void PrintProcessingRetainedResourcePressureSummary(
        string prefix,
        RadarProcessingRetainedResourcePressureSummary telemetry)
    {
        Console.WriteLine($"{prefix} current pending retained batches: {FormatNumber(telemetry.CurrentPendingRetainedBatchCount)}");
        Console.WriteLine($"{prefix} current pending retained payload bytes: {FormatNumber(telemetry.CurrentPendingRetainedPayloadBytes)}");
        Console.WriteLine($"{prefix} pending retained batches high watermark: {FormatNumber(telemetry.PendingRetainedBatchCountHighWatermark)}");
        Console.WriteLine($"{prefix} pending retained payload bytes high watermark: {FormatNumber(telemetry.PendingRetainedPayloadBytesHighWatermark)}");
        Console.WriteLine($"{prefix} current active retained batches: {FormatNumber(telemetry.CurrentActiveRetainedBatchCount)}");
        Console.WriteLine($"{prefix} current active retained payload bytes: {FormatNumber(telemetry.CurrentActiveRetainedPayloadBytes)}");
        Console.WriteLine($"{prefix} active retained batches high watermark: {FormatNumber(telemetry.ActiveRetainedBatchCountHighWatermark)}");
        Console.WriteLine($"{prefix} active retained payload bytes high watermark: {FormatNumber(telemetry.ActiveRetainedPayloadBytesHighWatermark)}");
        Console.WriteLine($"{prefix} current combined retained batches: {FormatNumber(telemetry.CurrentCombinedRetainedBatchCount)}");
        Console.WriteLine($"{prefix} current combined retained payload bytes: {FormatNumber(telemetry.CurrentCombinedRetainedPayloadBytes)}");
        Console.WriteLine($"{prefix} combined retained batches high watermark: {FormatNumber(telemetry.CombinedRetainedBatchCountHighWatermark)}");
        Console.WriteLine($"{prefix} combined retained payload bytes high watermark: {FormatNumber(telemetry.CombinedRetainedPayloadBytesHighWatermark)}");
    }

    public static void PrintProcessingRebalanceMovePressures(
        IReadOnlyList<RadarProcessingSyntheticRebalanceMovePressure> acceptedMovePressures)
    {
        const int displayedMovePressureLimit = 8;

        if (acceptedMovePressures.Count == 0)
        {
            Console.WriteLine("Accepted move pressures: (none)");
            return;
        }

        Console.WriteLine("Accepted move pressures:");
        var displayedCount = Math.Min(acceptedMovePressures.Count, displayedMovePressureLimit);
        for (var i = 0; i < displayedCount; i++)
        {
            var pressure = acceptedMovePressures[i];
            Console.WriteLine(
                $"  {FormatNumber(i + 1)}. {FormatProcessingRebalanceMoveKind(pressure.MoveKind)} " +
                $"source {FormatDecimal(pressure.SourceShardBefore)}->{FormatDecimal(pressure.SourceShardAfter)}, " +
                $"target {FormatDecimal(pressure.TargetShardBefore)}->{FormatDecimal(pressure.TargetShardAfter)}, " +
                $"relief {FormatDecimal(pressure.ExpectedRelief)}");
        }

        var omittedCount = acceptedMovePressures.Count - displayedCount;
        if (omittedCount > 0)
        {
            Console.WriteLine($"  ... {FormatNumber(omittedCount)} more accepted move pressure samples omitted");
        }
    }

    public static void PrintProcessingRebalanceRetentionStats(
        RadarProcessingRebalanceRetentionStats stats)
    {
        Console.WriteLine($"Retained decisions: {FormatNumber(stats.RetainedDecisionCount)}");
        Console.WriteLine($"Dropped decision details: {FormatNumber(stats.DroppedDecisionCount)}");
        Console.WriteLine($"Retained lifecycle transitions: {FormatNumber(stats.RetainedLifecycleTransitionCount)}");
        Console.WriteLine($"Dropped lifecycle transition details: {FormatNumber(stats.DroppedLifecycleTransitionCount)}");
        Console.WriteLine($"Retained accepted moves: {FormatNumber(stats.RetainedAcceptedMoveCount)}");
        Console.WriteLine($"Dropped accepted move details: {FormatNumber(stats.DroppedAcceptedMoveCount)}");
        Console.WriteLine($"Retained validation failures: {FormatNumber(stats.RetainedValidationFailureCount)}");
        Console.WriteLine($"Dropped validation failure details: {FormatNumber(stats.DroppedValidationFailureCount)}");
    }

    public static void PrintProcessingQuarantineLifecycle(
        int quarantineTtlEvaluations,
        int sustainedCoolingSampleCount,
        double materialPressureChangeThreshold)
    {
        Console.WriteLine($"Quarantine TTL evaluations: {FormatNumber(quarantineTtlEvaluations)}");
        Console.WriteLine($"Quarantine sustained cooling samples: {FormatNumber(sustainedCoolingSampleCount)}");
        Console.WriteLine($"Quarantine material pressure change: {FormatDecimal(materialPressureChangeThreshold)}");
    }

    public static void PrintProcessingPressureSkew(
        RadarProcessingPressureSkewOptions options)
    {
        Console.WriteLine($"Synthetic pressure overlay: {FormatBoolean(options.IsEnabled)}");
        Console.WriteLine($"Pressure skew profile: {FormatProcessingPressureSkewProfile(options.Profile)}");
        Console.WriteLine($"Pressure skew factor: {FormatDecimal(options.Factor)}");
        Console.WriteLine($"Pressure skew period: {FormatNumber(options.Period)}");
    }

    public static string FormatProcessingMode(RadarProcessingExecutionMode executionMode) =>
        executionMode switch
        {
            RadarProcessingExecutionMode.Sequential => "sequential",
            RadarProcessingExecutionMode.PartitionedBarrier => "partitioned",
            RadarProcessingExecutionMode.AsyncShardTransport => "async",
            _ => executionMode.ToString()
        };

    public static string FormatProcessingHandlerSet(RadarProcessingBenchmarkHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarProcessingBenchmarkHandlerSet.None => "none",
            RadarProcessingBenchmarkHandlerSet.CounterChecksum => "counter-checksum",
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy => "counter-checksum-heavy",
            _ => handlerSet.ToString()
        };

    public static string FormatProcessingRebalanceWorkload(RadarProcessingSyntheticRebalanceWorkloadKind workloadKind) =>
        workloadKind switch
        {
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => "balanced",
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => "hot-shard",
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => "intrinsic-hot",
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => "oscillating",
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => "cooldown-storm",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry => "quarantine-ttl-retry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear =>
                "quarantine-cooling-clear",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry =>
                "quarantine-pressure-change-retry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry =>
                "quarantine-retry-reentry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear =>
                "quarantine-successful-relief-clear",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard => "long-no-hot-shard",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection => "long-cooldown-rejection",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection =>
                "long-unsafe-target-rejection",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons =>
                "long-mixed-skipped-reasons",
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention => "counters-only-retention",
            _ => workloadKind.ToString()
        };

    public static string FormatProcessingRebalanceMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode) =>
        mode switch
        {
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance => "static",
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly => "sampling",
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession => "rebalance-session",
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession => "ordered-rebalance-session",
            _ => mode.ToString()
        };

    public static string FormatProcessingValidationProfile(RadarProcessingValidationProfile profile) =>
        profile switch
        {
            RadarProcessingValidationProfile.Off => "off",
            RadarProcessingValidationProfile.Essential => "essential",
            RadarProcessingValidationProfile.Diagnostic => "diagnostic",
            RadarProcessingValidationProfile.Benchmark => "benchmark",
            _ => profile.ToString()
        };

    public static string FormatProcessingRetentionMode(RadarProcessingDiagnosticRetentionMode retentionMode) =>
        retentionMode switch
        {
            RadarProcessingDiagnosticRetentionMode.Counters => "counters",
            RadarProcessingDiagnosticRetentionMode.Recent => "recent",
            RadarProcessingDiagnosticRetentionMode.Diagnostic => "diagnostic",
            _ => retentionMode.ToString()
        };

    public static string FormatProcessingPressureSkewProfile(RadarProcessingPressureSkewProfile profile) =>
        profile switch
        {
            RadarProcessingPressureSkewProfile.None => "none",
            RadarProcessingPressureSkewProfile.HotShard => "hot-shard",
            RadarProcessingPressureSkewProfile.RotatingHotShard => "rotating-hot-shard",
            RadarProcessingPressureSkewProfile.HotPartition => "hot-partition",
            RadarProcessingPressureSkewProfile.TargetStarvation => "target-starvation",
            RadarProcessingPressureSkewProfile.BudgetStorm => "budget-storm",
            _ => profile.ToString()
        };

    public static string FormatProcessingArchiveProviderMode(RadarProcessingArchiveProviderMode providerMode) =>
        providerMode switch
        {
            RadarProcessingArchiveProviderMode.BlockingBorrowed => "blocking-borrowed",
            RadarProcessingArchiveProviderMode.QueuedOwned => "queued-owned",
            _ => providerMode.ToString()
        };

    public static string FormatProcessingProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode providerOverlapMode) =>
        providerOverlapMode switch
        {
            RadarProcessingQueuedProviderOverlapMode.None => "none",
            RadarProcessingQueuedProviderOverlapMode.ProducerConsumer => "producer-consumer",
            _ => providerOverlapMode.ToString()
        };

    public static string FormatProcessingRetentionStrategy(RadarProcessingRetainedPayloadStrategy retentionStrategy) =>
        retentionStrategy switch
        {
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy => "snapshot-copy",
            RadarProcessingRetainedPayloadStrategy.PooledCopy => "pooled-copy",
            RadarProcessingRetainedPayloadStrategy.BuilderTransfer => "builder-transfer",
            _ => retentionStrategy.ToString()
        };

    public static string FormatProcessingBenchmarkApplicableOptionValueSource(
        ProcessingBenchmarkOptionValueSource source,
        bool isApplicable) =>
        isApplicable
            ? FormatProcessingBenchmarkOptionValueSource(source)
            : "not-applicable";

    public static string FormatProcessingBenchmarkOptionValueSource(ProcessingBenchmarkOptionValueSource source) =>
        source switch
        {
            ProcessingBenchmarkOptionValueSource.CurrentDefault => "current-default",
            ProcessingBenchmarkOptionValueSource.Explicit => "explicit",
            ProcessingBenchmarkOptionValueSource.RolloutDefault => "rollout-default",
            _ => source.ToString()
        };

    public static string FormatProviderQueueTelemetryOutput(ProcessingBenchmarkProviderQueueTelemetryOutput output) =>
        output switch
        {
            ProcessingBenchmarkProviderQueueTelemetryOutput.None => "none",
            ProcessingBenchmarkProviderQueueTelemetryOutput.Summary => "summary",
            ProcessingBenchmarkProviderQueueTelemetryOutput.Recent => "recent",
            _ => output.ToString()
        };

    public static string FormatProviderOverlapTelemetryOutput(ProcessingBenchmarkProviderOverlapTelemetryOutput output) =>
        output switch
        {
            ProcessingBenchmarkProviderOverlapTelemetryOutput.None => "none",
            ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary => "summary",
            ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent => "recent",
            _ => output.ToString()
        };

    public static string FormatProcessingProviderQueueRecentDetailKind(RadarProcessingProviderQueueRecentDetailKind kind) =>
        kind switch
        {
            RadarProcessingProviderQueueRecentDetailKind.Enqueue => "enqueue",
            RadarProcessingProviderQueueRecentDetailKind.Dequeue => "dequeue",
            RadarProcessingProviderQueueRecentDetailKind.Processing => "processing",
            _ => kind.ToString()
        };

    public static string FormatProcessingProviderQueueSequence(RadarProcessingQueuedBatchSequence? sequence) =>
        sequence.HasValue
            ? FormatNumber(sequence.Value.Value)
            : "n/a";

    public static string FormatProcessingProviderQueueEnqueueStatus(RadarProcessingQueuedBatchEnqueueStatus? status) =>
        status switch
        {
            RadarProcessingQueuedBatchEnqueueStatus.Accepted => "accepted",
            RadarProcessingQueuedBatchEnqueueStatus.Full => "full",
            RadarProcessingQueuedBatchEnqueueStatus.TimedOut => "timed-out",
            RadarProcessingQueuedBatchEnqueueStatus.Canceled => "canceled",
            RadarProcessingQueuedBatchEnqueueStatus.Closed => "closed",
            RadarProcessingQueuedBatchEnqueueStatus.Faulted => "faulted",
            null => "n/a",
            _ => status.Value.ToString()
        };

    public static string FormatProcessingProviderQueueProcessingStatus(RadarProcessingQueuedBatchProcessingStatus? status) =>
        status switch
        {
            RadarProcessingQueuedBatchProcessingStatus.Succeeded => "succeeded",
            RadarProcessingQueuedBatchProcessingStatus.FailedProcessing => "failed-processing",
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation => "failed-validation",
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration => "failed-migration",
            RadarProcessingQueuedBatchProcessingStatus.Canceled => "canceled",
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault => "skipped-after-fault",
            null => "n/a",
            _ => status.Value.ToString()
        };

    public static string FormatProcessingArchiveQueuedOverlapStatus(RadarProcessingArchiveQueuedOverlapStatus status) =>
        status switch
        {
            RadarProcessingArchiveQueuedOverlapStatus.NotStarted => "not-started",
            RadarProcessingArchiveQueuedOverlapStatus.Completed => "completed",
            RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed => "producer-failed",
            RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted => "consumer-faulted",
            RadarProcessingArchiveQueuedOverlapStatus.Canceled => "canceled",
            RadarProcessingArchiveQueuedOverlapStatus.Disposed => "disposed",
            _ => status.ToString()
        };

    public static string FormatProcessingQueuedSessionStatus(RadarProcessingQueuedSessionStatus status) =>
        status switch
        {
            RadarProcessingQueuedSessionStatus.NotStarted => "not-started",
            RadarProcessingQueuedSessionStatus.Running => "running",
            RadarProcessingQueuedSessionStatus.Draining => "draining",
            RadarProcessingQueuedSessionStatus.Completed => "completed",
            RadarProcessingQueuedSessionStatus.Faulted => "faulted",
            RadarProcessingQueuedSessionStatus.Canceled => "canceled",
            RadarProcessingQueuedSessionStatus.Disposed => "disposed",
            _ => status.ToString()
        };

    public static string FormatBoolean(bool value) =>
        value ? "yes" : "no";

    public static string FormatProcessingRebalanceMoveKind(RadarProcessingRebalanceMoveKind moveKind) =>
        moveKind switch
        {
            RadarProcessingRebalanceMoveKind.DirectHotRelief => "direct-hot-relief",
            RadarProcessingRebalanceMoveKind.ColdEvacuation => "cold-evacuation",
            RadarProcessingRebalanceMoveKind.RoomMakingReserved => "room-making-reserved",
            _ => moveKind.ToString()
        };

    public static string FormatProcessingRebalanceSkippedReasons(
        IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons) =>
        skippedReasons.Count == 0
            ? "(none)"
            : string.Join(", ", skippedReasons.Select(FormatProcessingRebalanceSkippedReason));

    public static string FormatProcessingRebalanceSkippedReasonCounters(
        IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> counters) =>
        counters.Count == 0
            ? "(none)"
            : string.Join(", ", counters.Select(counter =>
                $"{FormatProcessingRebalanceSkippedReason(counter.Reason)}={FormatNumber(counter.Count)}"));

    public static string FormatProcessingRebalanceSkippedReason(RadarProcessingRebalanceSkippedReason reason) =>
        reason switch
        {
            RadarProcessingRebalanceSkippedReason.NoSustainedPressure => "no-sustained-pressure",
            RadarProcessingRebalanceSkippedReason.NoHotShard => "no-hot-shard",
            RadarProcessingRebalanceSkippedReason.NoColdTargetShard => "no-cold-target-shard",
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget =>
                "direct-hot-partition-has-no-safe-target",
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit => "insufficient-projected-benefit",
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm => "target-would-become-warm",
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot => "target-would-become-hot",
            RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded => "target-headroom-exceeded",
            RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown => "candidate-partition-in-cooldown",
            RadarProcessingRebalanceSkippedReason.CandidatePartitionBelowMinimumResidency =>
                "candidate-partition-below-minimum-residency",
            RadarProcessingRebalanceSkippedReason.SourceShardInCooldown => "source-shard-in-cooldown",
            RadarProcessingRebalanceSkippedReason.TargetShardInCooldown => "target-shard-in-cooldown",
            RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted =>
                "source-shard-move-budget-exhausted",
            RadarProcessingRebalanceSkippedReason.TargetShardReceiveBudgetExhausted =>
                "target-shard-receive-budget-exhausted",
            RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted => "global-move-budget-exhausted",
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot =>
                "partition-classified-intrinsic-hot",
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined => "partition-quarantined",
            RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit =>
                "cold-evacuation-insufficient-benefit",
            RadarProcessingRebalanceSkippedReason.MigrationValidationFailed => "migration-validation-failed",
            _ => reason.ToString()
        };

}
