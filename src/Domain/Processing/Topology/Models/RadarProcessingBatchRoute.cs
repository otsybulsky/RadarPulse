namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingBatchRoute
{
    private readonly RadarProcessingRoutedEvent[] routedEvents;
    private readonly RadarProcessingPartitionBatchRoute[] partitions;
    private readonly RadarProcessingShardBatchRoute[] shards;

    internal RadarProcessingBatchRoute(
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRoutedEvent[] routedEvents,
        RadarProcessingPartitionBatchRoute[] partitions,
        RadarProcessingShardBatchRoute[] shards,
        RadarProcessingRouteMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(routedEvents);
        ArgumentNullException.ThrowIfNull(partitions);
        ArgumentNullException.ThrowIfNull(shards);

        TopologyVersion = topologyVersion;
        this.routedEvents = routedEvents;
        this.partitions = partitions;
        this.shards = shards;
        Metrics = metrics;
    }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public int EventCount => routedEvents.Length;

    public int PartitionCount => partitions.Length;

    public int ShardCount => shards.Length;

    public RadarProcessingRouteMetrics Metrics { get; }

    public ReadOnlyMemory<RadarProcessingRoutedEvent> RoutedEvents => routedEvents;

    public IReadOnlyList<RadarProcessingPartitionBatchRoute> Partitions => partitions;

    public IReadOnlyList<RadarProcessingShardBatchRoute> Shards => shards;

    public RadarProcessingRoutedEvent GetRoutedEvent(int eventIndex)
    {
        if ((uint)eventIndex < (uint)routedEvents.Length)
        {
            return routedEvents[eventIndex];
        }

        throw new ArgumentOutOfRangeException(nameof(eventIndex));
    }

    public RadarProcessingPartitionBatchRoute GetPartition(int partitionId)
    {
        if ((uint)partitionId < (uint)partitions.Length)
        {
            return partitions[partitionId];
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }

    public RadarProcessingShardBatchRoute GetShard(int shardId)
    {
        if ((uint)shardId < (uint)shards.Length)
        {
            return shards[shardId];
        }

        throw new ArgumentOutOfRangeException(nameof(shardId));
    }
}
