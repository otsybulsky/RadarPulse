using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    private static RadarProcessingBatchRoute CreateRoute()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 4,
            shardCount: 2);

        return new RadarProcessingBatchRouter(core.Topology).Route(CreateEmptyBatch(universe.Version));
    }

    private static IReadOnlyList<RadarProcessingAsyncWorkItem> CreateCanonicalWorkItems(
        RadarProcessingBatchRoute route) =>
        CreateWorkItems(
            route,
            new[]
            {
                new[] { 0, 1 },
                new[] { 2, 3 }
            });

    private static IReadOnlyList<RadarProcessingAsyncWorkItem> CreateWorkItems(
        RadarProcessingBatchRoute route,
        IReadOnlyList<int[]> partitionIds)
    {
        var scope = new RadarProcessingAsyncBatchScope(
            batchSequence: 1,
            route.TopologyVersion,
            expectedWorkItemCount: partitionIds.Count);
        var workItems = new RadarProcessingAsyncWorkItem[partitionIds.Count];
        for (var workItemId = 0; workItemId < workItems.Length; workItemId++)
        {
            workItems[workItemId] = scope.CreateWorkItem(
                workItemId,
                new RadarProcessingWorkerId(workItemId),
                shardId: workItemId,
                partitionIds[workItemId]);
        }

        return Array.AsReadOnly(workItems);
    }

    private static RadarProcessingAsyncBatchScopeResult CreateBatchResult(
        RadarProcessingBatchRoute route,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        int failedWorkItemId = -1,
        RadarProcessingAsyncBatchCompletionError error = RadarProcessingAsyncBatchCompletionError.None)
    {
        var completions = new RadarProcessingAsyncWorkCompletion[workItems.Count];
        for (var i = 0; i < completions.Length; i++)
        {
            var workItem = workItems[i];
            var shard = route.GetShard(workItem.ShardId);
            completions[i] = i == failedWorkItemId
                ? RadarProcessingAsyncWorkCompletion.Failed(workItem)
                : RadarProcessingAsyncWorkCompletion.Succeeded(
                    workItem,
                    processedStreamEventCount: shard.Metrics.EventCount,
                    processedPayloadValueCount: shard.Metrics.PayloadValueCount);
        }

        return new RadarProcessingAsyncBatchScopeResult(
            new RadarProcessingAsyncBatchCompletion(
                batchSequence: 1,
                route.TopologyVersion,
                workItems.Count,
                completions,
                isClosed: true),
            error);
    }
}
