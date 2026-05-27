namespace RadarPulse.Domain.Processing;

/// <summary>
/// Complete routing plan for a batch under one topology version.
/// </summary>
/// <remarks>
/// Event indexes always refer to positions in the original batch. The route is
/// grouped both by partition and by shard so processing can dispatch work while
/// validators can verify that telemetry was generated from the same ownership map.
/// </remarks>
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

    /// <summary>
    /// Topology version used to produce this route.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Number of events routed from the original batch.
    /// </summary>
    public int EventCount => routedEvents.Length;

    /// <summary>
    /// Number of partition routes.
    /// </summary>
    public int PartitionCount => partitions.Length;

    /// <summary>
    /// Number of shard routes.
    /// </summary>
    public int ShardCount => shards.Length;

    /// <summary>
    /// Aggregate route metrics for all routed events.
    /// </summary>
    public RadarProcessingRouteMetrics Metrics { get; }

    /// <summary>
    /// Per-event route entries in original batch order.
    /// </summary>
    public ReadOnlyMemory<RadarProcessingRoutedEvent> RoutedEvents => routedEvents;

    /// <summary>
    /// Partition routes ordered by partition id.
    /// </summary>
    public IReadOnlyList<RadarProcessingPartitionBatchRoute> Partitions => partitions;

    /// <summary>
    /// Shard routes ordered by shard id.
    /// </summary>
    public IReadOnlyList<RadarProcessingShardBatchRoute> Shards => shards;

    /// <summary>
    /// Returns the route entry for an event index.
    /// </summary>
    public RadarProcessingRoutedEvent GetRoutedEvent(int eventIndex)
    {
        if ((uint)eventIndex < (uint)routedEvents.Length)
        {
            return routedEvents[eventIndex];
        }

        throw new ArgumentOutOfRangeException(nameof(eventIndex));
    }

    /// <summary>
    /// Returns the route for a partition id.
    /// </summary>
    public RadarProcessingPartitionBatchRoute GetPartition(int partitionId)
    {
        if ((uint)partitionId < (uint)partitions.Length)
        {
            return partitions[partitionId];
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }

    /// <summary>
    /// Returns the route for a shard id.
    /// </summary>
    public RadarProcessingShardBatchRoute GetShard(int shardId)
    {
        if ((uint)shardId < (uint)shards.Length)
        {
            return shards[shardId];
        }

        throw new ArgumentOutOfRangeException(nameof(shardId));
    }
}
