using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Builds async shard dispatch plans and submits them to a worker group.
/// </summary>
public sealed class RadarProcessingAsyncBatchDispatcher
{
    private readonly RadarProcessingAsyncWorkerGroup workerGroup;
    private readonly Func<RadarProcessingTopology> topologyProvider;

    /// <summary>
    /// Creates a dispatcher over a worker group and topology provider.
    /// </summary>
    public RadarProcessingAsyncBatchDispatcher(
        RadarProcessingAsyncWorkerGroup workerGroup,
        Func<RadarProcessingTopology> topologyProvider)
    {
        ArgumentNullException.ThrowIfNull(workerGroup);
        ArgumentNullException.ThrowIfNull(topologyProvider);

        this.workerGroup = workerGroup;
        this.topologyProvider = topologyProvider;
    }

    /// <summary>
    /// Captures the current topology and creates a dispatch plan for a batch.
    /// </summary>
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

    /// <summary>
    /// Creates a dispatch plan from an already captured topology and route.
    /// </summary>
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

    /// <summary>
    /// Creates a plan for a batch and dispatches its shard work items.
    /// </summary>
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
