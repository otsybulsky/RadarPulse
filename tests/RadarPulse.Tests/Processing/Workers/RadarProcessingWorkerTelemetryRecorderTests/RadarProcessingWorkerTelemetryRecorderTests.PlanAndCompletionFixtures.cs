using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerTelemetryRecorderTests
{
    private static RadarProcessingAsyncDispatchPlan CreatePlan(
        long batchSequence)
    {
        using var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1)));
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);
        return new RadarProcessingAsyncBatchDispatcher(group, () => topology).CreatePlan(batchSequence, batch);
    }

    private static RadarProcessingWorkerGroupStatus CreateHealthyStatus(
        RadarProcessingAsyncDispatchPlan plan) =>
        new(
            RadarProcessingWorkerGroupState.Running,
            RadarProcessingWorkerHealth.Healthy,
            plan.ShardCount,
            queueCapacity: 1);

    private static RadarProcessingAsyncWorkCompletion[] CreateSucceededCompletions(
        RadarProcessingAsyncDispatchPlan plan) =>
        plan.WorkItems
            .Select(workItem => CreateSucceededCompletion(plan, workItem.WorkItemId))
            .ToArray();

    private static RadarProcessingAsyncWorkCompletion CreateSucceededCompletion(
        RadarProcessingAsyncDispatchPlan plan,
        int workItemId)
    {
        var workItem = plan.WorkItems[workItemId];
        var shard = plan.Route.GetShard(workItem.ShardId);
        return RadarProcessingAsyncWorkCompletion.Succeeded(
            workItem,
            processedStreamEventCount: shard.EventIndexes.Length,
            processedPayloadValueCount: shard.Metrics.PayloadValueCount);
    }

    private static RadarProcessingAsyncBatchScopeResult CreateBatchResult(
        RadarProcessingAsyncDispatchPlan plan,
        IReadOnlyCollection<RadarProcessingAsyncWorkCompletion> completions,
        RadarProcessingAsyncBatchCompletionError error = RadarProcessingAsyncBatchCompletionError.None) =>
        new(
            new RadarProcessingAsyncBatchCompletion(
                plan.BatchSequence,
                plan.TopologyVersion,
                plan.ExpectedWorkItemCount,
                completions,
                isClosed: true),
            error);

    private static RadarProcessingAsyncWorkerGroup CreateStartedGroup(
        int workerCount,
        int queueCapacity)
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: workerCount, queueCapacity: queueCapacity)));
        Assert.True(group.Start().IsSuccess);
        return group;
    }
}
