namespace RadarPulse.Domain.Processing;

/// <summary>
/// Validates async processing results, transport assignment, deterministic parity, and retained worker telemetry.
/// </summary>
public static class RadarProcessingAsyncValidator
{
    /// <summary>
    /// Validates an async shard transport processing result according to the selected profile.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateProcessingResult(
        RadarProcessingResult result,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(result);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        if (result.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.NonAsyncExecutionMode,
                "Async validation requires an async shard transport processing result.",
                validationProfile);
        }

        var essential = ValidateProcessingResultEssential(result, validationProfile);
        if (!essential.IsValid ||
            validationProfile == RadarProcessingValidationProfile.Essential)
        {
            return essential;
        }

        var diagnostic = ValidateProcessingResultDiagnostic(result, validationProfile);
        return diagnostic.IsValid ||
               validationProfile == RadarProcessingValidationProfile.Diagnostic ||
               validationProfile == RadarProcessingValidationProfile.Benchmark
            ? diagnostic
            : RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    /// <summary>
    /// Validates an async rebalance result and ensures failed processing does not publish migration artifacts.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateRebalanceResult(
        RadarProcessingRebalanceSessionResult result,
        RadarProcessingTopology? currentTopology = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(result);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        var processing = ValidateProcessingResult(result.ProcessingResult, validationProfile);
        if (!processing.IsValid)
        {
            return processing;
        }

        if (!result.ProcessingResult.IsValid &&
            (result.PressureSample is not null ||
             result.DirectHotReliefDecision is not null ||
             result.ColdEvacuationDecision is not null ||
             result.MigrationResult is not null ||
             result.HandoffValidation is not null ||
             result.PublishedMigration))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.UnexpectedMigrationAfterFailedProcessing,
                "Failed async processing must not evaluate or publish rebalance artifacts.",
                validationProfile);
        }

        if (currentTopology is not null &&
            result.MigrationResult is not null &&
            result.MigrationResult.CurrentTopologyVersion != currentTopology.Version)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async rebalance migration topology version must match the current topology.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    /// <summary>
    /// Validates async work item coverage, completion scope, worker assignment, and aggregate metrics.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateTransport(
        RadarProcessingBatchRoute route,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        RadarProcessingAsyncBatchScopeResult? batchResult,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic,
        int? workerCount = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(workItems);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);
        if (workerCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount.Value);
        }

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        var essential = ValidateTransportEssential(route, workItems, batchResult, validationProfile);
        if (!essential.IsValid ||
            validationProfile == RadarProcessingValidationProfile.Essential)
        {
            return essential;
        }

        return ValidateTransportDiagnostic(route, workItems, batchResult!, validationProfile, workerCount);
    }

    /// <summary>
    /// Compares synchronous and async outputs for benchmark-grade deterministic parity.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateBenchmarkComparison(
        RadarProcessingResult synchronousResult,
        RadarProcessingResult asyncResult,
        IReadOnlyList<RadarSourceProcessingSnapshot>? synchronousSnapshots = null,
        IReadOnlyList<RadarSourceProcessingSnapshot>? asyncSnapshots = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Benchmark)
    {
        ArgumentNullException.ThrowIfNull(synchronousResult);
        ArgumentNullException.ThrowIfNull(asyncResult);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile != RadarProcessingValidationProfile.Benchmark)
        {
            return ValidateProcessingResult(asyncResult, validationProfile);
        }

        if (synchronousResult.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.NonAsyncExecutionMode,
                "Benchmark comparison requires a synchronous reference result.",
                validationProfile,
                synchronousResult.Metrics.ProcessingChecksum,
                asyncResult.Metrics.ProcessingChecksum);
        }

        var asyncValidation = ValidateProcessingResult(asyncResult, validationProfile);
        if (!asyncValidation.IsValid)
        {
            return asyncValidation;
        }

        if (synchronousResult.IsValid != asyncResult.IsValid ||
            synchronousResult.Metrics != asyncResult.Metrics)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.DeterministicChecksumMismatch,
                "Synchronous and async processing metrics do not match.",
                validationProfile,
                synchronousResult.Metrics.ProcessingChecksum,
                asyncResult.Metrics.ProcessingChecksum);
        }

        if (synchronousSnapshots is not null || asyncSnapshots is not null)
        {
            if (synchronousSnapshots is null ||
                asyncSnapshots is null ||
                synchronousSnapshots.Count != asyncSnapshots.Count)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.DeterministicChecksumMismatch,
                    "Synchronous and async processing snapshot counts do not match.",
                    validationProfile,
                    synchronousResult.Metrics.ProcessingChecksum,
                    asyncResult.Metrics.ProcessingChecksum);
            }

            for (var sourceId = 0; sourceId < synchronousSnapshots.Count; sourceId++)
            {
                if (synchronousSnapshots[sourceId] != asyncSnapshots[sourceId])
                {
                    return Invalid(
                        RadarProcessingAsyncValidationError.DeterministicChecksumMismatch,
                        "Synchronous and async source snapshots do not match.",
                        validationProfile,
                        synchronousResult.Metrics.ProcessingChecksum,
                        asyncResult.Metrics.ProcessingChecksum);
                }
            }
        }

        return RadarProcessingAsyncValidationResult.Valid(
            validationProfile,
            synchronousResult.Metrics.ProcessingChecksum,
            asyncResult.Metrics.ProcessingChecksum);
    }

    /// <summary>
    /// Validates that retained worker telemetry obeys configured retention limits and counters.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateWorkerTelemetryRetention(
        RadarProcessingWorkerTelemetrySummary workerTelemetry,
        RadarProcessingTelemetryRetentionOptions retentionOptions,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(workerTelemetry);
        ArgumentNullException.ThrowIfNull(retentionOptions);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        var expectedBatchLimit = retentionOptions.RetentionMode == RadarProcessingDiagnosticRetentionMode.Counters
            ? 0
            : retentionOptions.MaxRetainedWorkerBatches;
        var expectedFailureLimit = retentionOptions.RetentionMode == RadarProcessingDiagnosticRetentionMode.Counters
            ? 0
            : retentionOptions.MaxRetainedWorkerFailures;

        if (workerTelemetry.RecentBatches.Count > expectedBatchLimit ||
            workerTelemetry.RecentFailures.Count > expectedFailureLimit)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.RetentionLimitExceeded,
                "Async worker telemetry detail exceeds the configured retention limits.",
                validationProfile);
        }

        if (workerTelemetry.RetentionStats.RetainedBatchCount != workerTelemetry.RecentBatches.Count ||
            workerTelemetry.RetentionStats.RetainedFailureCount != workerTelemetry.RecentFailures.Count)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.RetentionLimitExceeded,
                "Async worker telemetry retention stats do not match retained detail counts.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    private static RadarProcessingAsyncValidationResult ValidateProcessingResultEssential(
        RadarProcessingResult result,
        RadarProcessingValidationProfile validationProfile)
    {
        if (result.IsValid)
        {
            if (result.Telemetry is null)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.MissingProcessingTelemetry,
                    "Valid async processing results must carry processing telemetry.",
                    validationProfile);
            }

            if (result.WorkerTelemetry is null ||
                result.WorkerTelemetry.Counters.DispatchedBatchCount == 0 ||
                result.WorkerTelemetry.Counters.CompletedBatchCount == 0)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.MissingWorkerTelemetry,
                    "Valid async processing results must carry completed worker telemetry.",
                    validationProfile);
            }
        }
        else if (result.WorkerTelemetry is not null &&
                 result.WorkerTelemetry.Counters.FailedBatchCount == 0 &&
                 result.WorkerTelemetry.Counters.CanceledBatchCount == 0 &&
                 result.WorkerTelemetry.Counters.RejectedDispatchCount == 0 &&
                 result.WorkerTelemetry.Counters.FailedWorkItemCount == 0 &&
                 result.WorkerTelemetry.Counters.CanceledWorkItemCount == 0)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.WorkerFailureNotPropagated,
                "Invalid async processing results with worker telemetry must report a worker failure, cancellation, or rejected dispatch.",
                validationProfile);
        }

        if (result.Telemetry is not null &&
            result.Telemetry.TopologyVersion != result.TopologyVersion)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async processing result topology version must match processing telemetry.",
                validationProfile);
        }

        var latestWorkerBatch = GetLatestWorkerBatch(result.WorkerTelemetry);
        if (latestWorkerBatch is not null &&
            latestWorkerBatch.TopologyVersion != result.TopologyVersion)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async worker telemetry topology version must match the processing result.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    private static RadarProcessingAsyncValidationResult ValidateProcessingResultDiagnostic(
        RadarProcessingResult result,
        RadarProcessingValidationProfile validationProfile)
    {
        if (result.Telemetry is not null &&
            (SumPartitionMetrics(result.Telemetry) != result.Telemetry.BatchMetrics ||
             SumShardMetrics(result.Telemetry) != result.Telemetry.BatchMetrics))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TelemetryMetricMismatch,
                "Async processing telemetry partition or shard totals do not match batch metrics.",
                validationProfile);
        }

        var latestWorkerBatch = GetLatestWorkerBatch(result.WorkerTelemetry);
        if (latestWorkerBatch is null)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        if (latestWorkerBatch.SubmittedWorkItemCount != result.ShardCount)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.MissingWorkItem,
                "Async worker telemetry submitted work item count must match the result shard count.",
                validationProfile);
        }

        if (result.IsValid &&
            (!latestWorkerBatch.IsSuccessful ||
             latestWorkerBatch.SucceededWorkItemCount != result.ShardCount))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.CompletionStatusMismatch,
                "Valid async processing results require successful completion for every shard work item.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    private static RadarProcessingAsyncValidationResult ValidateTransportEssential(
        RadarProcessingBatchRoute route,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        RadarProcessingAsyncBatchScopeResult? batchResult,
        RadarProcessingValidationProfile validationProfile)
    {
        if (batchResult is null)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.IncompleteBatchCompletion,
                "Async transport validation requires a batch completion result.",
                validationProfile);
        }

        if (batchResult.Completion.TopologyVersion != route.TopologyVersion)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async batch completion topology version must match the route.",
                validationProfile);
        }

        if (batchResult.Completion.ExpectedWorkItemCount != workItems.Count)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.MissingWorkItem,
                "Async batch completion expected work item count must match submitted work items.",
                validationProfile);
        }

        if (!batchResult.Completion.IsComplete)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.IncompleteBatchCompletion,
                "Async batch completion must include every submitted work item.",
                validationProfile);
        }

        if (!batchResult.IsSuccess)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.FailedBatchCompletion,
                "Async batch completion failed or was canceled.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    private static RadarProcessingAsyncValidationResult ValidateTransportDiagnostic(
        RadarProcessingBatchRoute route,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        RadarProcessingAsyncBatchScopeResult batchResult,
        RadarProcessingValidationProfile validationProfile,
        int? workerCount)
    {
        var workItemById = new RadarProcessingAsyncWorkItem[workItems.Count];
        var seenWorkItems = new bool[workItems.Count];
        var seenPartitions = new bool[route.PartitionCount];

        foreach (var workItem in workItems)
        {
            ArgumentNullException.ThrowIfNull(workItem, nameof(workItems));

            if ((uint)workItem.WorkItemId >= (uint)workItems.Count ||
                seenWorkItems[workItem.WorkItemId])
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.MissingWorkItem,
                    "Async work item ids must be unique and cover the submitted work item count.",
                    validationProfile);
            }

            seenWorkItems[workItem.WorkItemId] = true;
            workItemById[workItem.WorkItemId] = workItem;

            if (workItem.TopologyVersion != route.TopologyVersion)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.WorkItemScopeMismatch,
                    "Async work item topology version must match the route.",
                    validationProfile);
            }

            if ((uint)workItem.ShardId >= (uint)route.ShardCount)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch,
                    "Async work item shard id must be inside the route shard range.",
                    validationProfile);
            }

            if (workerCount.HasValue &&
                (uint)workItem.WorkerId.Value >= (uint)workerCount.Value)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.WorkItemWorkerAssignmentMismatch,
                    "Async work item worker id must be inside the configured worker range.",
                    validationProfile);
            }

            foreach (var partitionId in workItem.PartitionIds)
            {
                if ((uint)partitionId >= (uint)route.PartitionCount)
                {
                    return Invalid(
                        RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch,
                        "Async work item partition id must be inside the route partition range.",
                        validationProfile);
                }

                if (seenPartitions[partitionId])
                {
                    return Invalid(
                        RadarProcessingAsyncValidationError.DuplicateWorkAssignment,
                        "Async work item partition assignments must not overlap.",
                        validationProfile);
                }

                seenPartitions[partitionId] = true;
                if (route.GetPartition(partitionId).ShardId != workItem.ShardId)
                {
                    return Invalid(
                        RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch,
                        "Async work item partition assignments must match route shard ownership.",
                        validationProfile);
                }
            }
        }

        if (seenWorkItems.Any(static seen => !seen) ||
            seenPartitions.Any(static seen => !seen))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.MissingWorkItem,
                "Async work items must cover every work item id and route partition exactly once.",
                validationProfile);
        }

        var processedStreamEventCount = 0L;
        var processedPayloadValueCount = 0L;
        foreach (var completion in batchResult.Completion.Completions)
        {
            if ((uint)completion.WorkItemId >= (uint)workItemById.Length)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.CompletionScopeMismatch,
                    "Async completion work item id must be inside the submitted work item range.",
                    validationProfile);
            }

            var workItem = workItemById[completion.WorkItemId];
            if (workItem is null ||
                completion.BatchSequence != workItem.BatchSequence ||
                completion.TopologyVersion != workItem.TopologyVersion ||
                completion.WorkerId != workItem.WorkerId)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.CompletionScopeMismatch,
                    "Async completion scope must match the submitted work item.",
                    validationProfile);
            }

            if (!completion.IsSuccessful)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.CompletionStatusMismatch,
                    "Successful async transport validation requires successful work completions.",
                    validationProfile);
            }

            var expectedShard = route.GetShard(workItem.ShardId);
            if (completion.ProcessedStreamEventCount != expectedShard.Metrics.EventCount ||
                completion.ProcessedPayloadValueCount != expectedShard.Metrics.PayloadValueCount)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.AggregationMetricMismatch,
                    "Async completion metrics must match the routed shard metrics.",
                    validationProfile);
            }

            processedStreamEventCount = checked(processedStreamEventCount + completion.ProcessedStreamEventCount);
            processedPayloadValueCount = checked(processedPayloadValueCount + completion.ProcessedPayloadValueCount);
        }

        if (processedStreamEventCount != route.Metrics.EventCount ||
            processedPayloadValueCount != route.Metrics.PayloadValueCount)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.AggregationMetricMismatch,
                "Async aggregated completion metrics must match route metrics.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    private static RadarProcessingRecentWorkerBatch? GetLatestWorkerBatch(
        RadarProcessingWorkerTelemetrySummary? workerTelemetry) =>
        workerTelemetry?.RecentBatches.Count > 0
            ? workerTelemetry.RecentBatches[^1]
            : null;

    private static RadarProcessingRouteMetrics SumPartitionMetrics(
        RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in telemetry.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(
        RadarProcessingTelemetry telemetry)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in telemetry.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingAsyncValidationResult Invalid(
        RadarProcessingAsyncValidationError error,
        string message,
        RadarProcessingValidationProfile validationProfile,
        ulong? synchronousChecksum = null,
        ulong? asyncChecksum = null) =>
        RadarProcessingAsyncValidationResult.Invalid(
            error,
            message,
            validationProfile,
            synchronousChecksum,
            asyncChecksum);
}
