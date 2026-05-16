namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingTelemetry
{
    private readonly IReadOnlyList<RadarProcessingPartitionTelemetry> partitions;
    private readonly IReadOnlyList<RadarProcessingShardTelemetry> shards;

    internal RadarProcessingTelemetry(
        RadarProcessingExecutionMode executionMode,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRouteMetrics batchMetrics,
        RadarProcessingPartitionTelemetry[] partitions,
        RadarProcessingShardTelemetry[] shards,
        int hotPartitionId,
        int hotShardId)
    {
        RadarProcessingCoreOptions.EnsureKnownExecutionMode(executionMode);
        ArgumentNullException.ThrowIfNull(partitions);
        ArgumentNullException.ThrowIfNull(shards);

        if (partitions.Length == 0)
        {
            throw new ArgumentException("Telemetry must contain at least one partition.", nameof(partitions));
        }

        if (shards.Length == 0)
        {
            throw new ArgumentException("Telemetry must contain at least one shard.", nameof(shards));
        }

        if (partitions.Length < shards.Length)
        {
            throw new ArgumentException("Partition telemetry count must be greater than or equal to shard count.");
        }

        ValidateHotId(hotPartitionId, partitions.Length, nameof(hotPartitionId));
        ValidateHotId(hotShardId, shards.Length, nameof(hotShardId));
        ValidatePartitionTelemetry(partitions, shards.Length);
        ValidateShardTelemetry(shards);
        ValidateRouteMetrics(batchMetrics, partitions, shards);

        ExecutionMode = executionMode;
        TopologyVersion = topologyVersion;
        BatchMetrics = batchMetrics;
        this.partitions = Array.AsReadOnly((RadarProcessingPartitionTelemetry[])partitions.Clone());
        this.shards = Array.AsReadOnly((RadarProcessingShardTelemetry[])shards.Clone());
        HotPartitionId = hotPartitionId;
        HotShardId = hotShardId;
    }

    public RadarProcessingExecutionMode ExecutionMode { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingRouteMetrics BatchMetrics { get; }

    public int PartitionCount => partitions.Count;

    public int ShardCount => shards.Count;

    public IReadOnlyList<RadarProcessingPartitionTelemetry> Partitions => partitions;

    public IReadOnlyList<RadarProcessingShardTelemetry> Shards => shards;

    public int HotPartitionId { get; }

    public int HotShardId { get; }

    internal static RadarProcessingTelemetry FromRoute(
        RadarProcessingExecutionMode executionMode,
        RadarProcessingBatchRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var partitions = CreatePartitionTelemetry(route);
        var shards = CreateShardTelemetry(route, partitions);

        return new RadarProcessingTelemetry(
            executionMode,
            route.TopologyVersion,
            route.Metrics,
            partitions,
            shards,
            ResolveHotPartitionId(partitions),
            ResolveHotShardId(shards));
    }

    private static RadarProcessingPartitionTelemetry[] CreatePartitionTelemetry(
        RadarProcessingBatchRoute route)
    {
        var result = new RadarProcessingPartitionTelemetry[route.PartitionCount];
        for (var partitionId = 0; partitionId < result.Length; partitionId++)
        {
            var partition = route.GetPartition(partitionId);
            result[partitionId] = new RadarProcessingPartitionTelemetry(
                partition.PartitionId,
                partition.ShardId,
                partition.Metrics);
        }

        return result;
    }

    private static RadarProcessingShardTelemetry[] CreateShardTelemetry(
        RadarProcessingBatchRoute route,
        RadarProcessingPartitionTelemetry[] partitions)
    {
        var partitionCounts = new int[route.ShardCount];
        var activePartitionCounts = new int[route.ShardCount];

        foreach (var partition in partitions)
        {
            partitionCounts[partition.ShardId]++;
            if (partition.HasWork)
            {
                activePartitionCounts[partition.ShardId]++;
            }
        }

        var result = new RadarProcessingShardTelemetry[route.ShardCount];
        for (var shardId = 0; shardId < result.Length; shardId++)
        {
            var shard = route.GetShard(shardId);
            result[shardId] = new RadarProcessingShardTelemetry(
                shard.ShardId,
                partitionCounts[shardId],
                activePartitionCounts[shardId],
                shard.Metrics);
        }

        return result;
    }

    private static int ResolveHotPartitionId(RadarProcessingPartitionTelemetry[] partitions)
    {
        var hotPartitionId = -1;
        var hotEventCount = 0L;

        foreach (var partition in partitions)
        {
            if (partition.EventCount > hotEventCount)
            {
                hotEventCount = partition.EventCount;
                hotPartitionId = partition.PartitionId;
            }
        }

        return hotPartitionId;
    }

    private static int ResolveHotShardId(RadarProcessingShardTelemetry[] shards)
    {
        var hotShardId = -1;
        var hotEventCount = 0L;

        foreach (var shard in shards)
        {
            if (shard.EventCount > hotEventCount)
            {
                hotEventCount = shard.EventCount;
                hotShardId = shard.ShardId;
            }
        }

        return hotShardId;
    }

    private static void ValidateHotId(
        int hotId,
        int count,
        string paramName)
    {
        if (hotId == -1 || (uint)hotId < (uint)count)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName);
    }

    private static void ValidatePartitionTelemetry(
        RadarProcessingPartitionTelemetry[] partitions,
        int shardCount)
    {
        for (var partitionId = 0; partitionId < partitions.Length; partitionId++)
        {
            var partition = partitions[partitionId];
            if (partition.PartitionId != partitionId)
            {
                throw new ArgumentException(
                    "Partition telemetry must be ordered by partition id.",
                    nameof(partitions));
            }

            if ((uint)partition.ShardId >= (uint)shardCount)
            {
                throw new ArgumentOutOfRangeException(nameof(partitions));
            }
        }
    }

    private static void ValidateShardTelemetry(
        RadarProcessingShardTelemetry[] shards)
    {
        for (var shardId = 0; shardId < shards.Length; shardId++)
        {
            if (shards[shardId].ShardId != shardId)
            {
                throw new ArgumentException(
                    "Shard telemetry must be ordered by shard id.",
                    nameof(shards));
            }
        }
    }

    private static void ValidateRouteMetrics(
        RadarProcessingRouteMetrics batchMetrics,
        RadarProcessingPartitionTelemetry[] partitions,
        RadarProcessingShardTelemetry[] shards)
    {
        var partitionMetrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in partitions)
        {
            partitionMetrics = partitionMetrics.Add(partition.Metrics);
        }

        if (partitionMetrics != batchMetrics)
        {
            throw new ArgumentException("Partition telemetry metrics must equal batch metrics.");
        }

        var shardMetrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in shards)
        {
            shardMetrics = shardMetrics.Add(shard.Metrics);
        }

        if (shardMetrics != batchMetrics)
        {
            throw new ArgumentException("Shard telemetry metrics must equal batch metrics.");
        }
    }
}
