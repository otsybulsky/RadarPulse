using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingAsyncBatchDispatcher
{
    private readonly RadarProcessingAsyncWorkerGroup workerGroup;
    private readonly Func<RadarProcessingTopology> topologyProvider;

    public RadarProcessingAsyncBatchDispatcher(
        RadarProcessingAsyncWorkerGroup workerGroup,
        Func<RadarProcessingTopology> topologyProvider)
    {
        ArgumentNullException.ThrowIfNull(workerGroup);
        ArgumentNullException.ThrowIfNull(topologyProvider);

        this.workerGroup = workerGroup;
        this.topologyProvider = topologyProvider;
    }

    public RadarProcessingAsyncDispatchPlan CreatePlan(
        long batchSequence,
        RadarEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var topology = topologyProvider();
        ArgumentNullException.ThrowIfNull(topology);

        var route = new RadarProcessingBatchRouter(topology).Route(batch);
        return CreatePlan(batchSequence, topology, route);
    }

    public RadarProcessingAsyncDispatchPlan CreatePlan(
        long batchSequence,
        RadarProcessingTopology topology,
        RadarProcessingBatchRoute route)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(route);

        if (route.TopologyVersion != topology.Version)
        {
            throw new ArgumentException("Route topology version must match the captured topology.", nameof(route));
        }

        if (route.PartitionCount != topology.PartitionCount)
        {
            throw new ArgumentException("Route partition count must match the captured topology.", nameof(route));
        }

        if (route.ShardCount != topology.ShardCount)
        {
            throw new ArgumentException("Route shard count must match the captured topology.", nameof(route));
        }

        var scope = new RadarProcessingAsyncBatchScope(
            batchSequence,
            topology.Version,
            topology.ShardCount);
        var workItems = CreateShardWorkItems(scope, topology);
        return new RadarProcessingAsyncDispatchPlan(scope, route, workItems);
    }

    public async ValueTask<RadarProcessingAsyncDispatchResult> DispatchAsync(
        long batchSequence,
        RadarEventBatch batch,
        RadarProcessingAsyncDispatchExecutor executor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(executor);

        var plan = CreatePlan(batchSequence, batch);
        var workerResult = await workerGroup.DispatchAsync(
            plan.Scope,
            plan.WorkItems,
            (workItem, workCancellationToken) => executor(
                batch,
                plan.Route,
                workItem,
                workCancellationToken),
            cancellationToken).ConfigureAwait(false);

        return new RadarProcessingAsyncDispatchResult(plan, workerResult);
    }

    private RadarProcessingAsyncWorkItem[] CreateShardWorkItems(
        RadarProcessingAsyncBatchScope scope,
        RadarProcessingTopology topology)
    {
        var workItems = new RadarProcessingAsyncWorkItem[topology.ShardCount];
        for (var shardId = 0; shardId < workItems.Length; shardId++)
        {
            workItems[shardId] = scope.CreateWorkItem(
                shardId,
                new RadarProcessingWorkerId(shardId % workerGroup.Options.WorkerCount),
                shardId,
                GetShardPartitionIds(topology, shardId));
        }

        return workItems;
    }

    private static int[] GetShardPartitionIds(
        RadarProcessingTopology topology,
        int shardId)
    {
        var partitions = new List<int>();
        foreach (var partition in topology.Partitions)
        {
            if (partition.ShardId == shardId)
            {
                partitions.Add(partition.PartitionId);
            }
        }

        return partitions.ToArray();
    }
}
