namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingAsyncValidator
{
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

}
