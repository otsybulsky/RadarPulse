using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Routes a radar event batch through a fixed processing topology snapshot.
/// </summary>
/// <remarks>
/// Routing validates source-universe compatibility, preserves original event
/// indexes, and builds per-partition and per-shard metrics that later pressure
/// and rebalance validation compare against processing telemetry.
/// </remarks>
public sealed class RadarProcessingBatchRouter
{
    private readonly RadarProcessingTopology topology;

    /// <summary>
    /// Creates a router bound to a topology snapshot.
    /// </summary>
    public RadarProcessingBatchRouter(RadarProcessingTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);

        this.topology = topology;
    }

    /// <summary>
    /// Topology snapshot used by this router.
    /// </summary>
    public RadarProcessingTopology Topology => topology;

    /// <summary>
    /// Builds a partition and shard route for the batch.
    /// </summary>
    /// <returns>
    /// Route containing per-event ownership, per-partition work lists, per-shard work
    /// lists, and aggregate payload metrics for the topology version.
    /// </returns>
    public RadarProcessingBatchRoute Route(RadarEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.SourceUniverseVersion != topology.SourceUniverseVersion)
        {
            throw new ArgumentException(
                "Batch source-universe version does not match the processing topology.",
                nameof(batch));
        }

        var events = batch.Events.Span;
        var partitionEventCounts = new int[topology.PartitionCount];
        var shardEventCounts = new int[topology.ShardCount];

        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            var partition = topology.GetPartitionForSource(events[eventIndex].SourceId);
            partitionEventCounts[partition.PartitionId]++;
            shardEventCounts[partition.ShardId]++;
        }

        var partitionEventIndexes = CreateEventIndexBuffers(partitionEventCounts);
        var shardEventIndexes = CreateEventIndexBuffers(shardEventCounts);
        var partitionOffsets = new int[topology.PartitionCount];
        var shardOffsets = new int[topology.ShardCount];
        var partitionMetrics = new RadarProcessingRouteMetrics[topology.PartitionCount];
        var shardMetrics = new RadarProcessingRouteMetrics[topology.ShardCount];
        var routedEvents = new RadarProcessingRoutedEvent[events.Length];
        var totalMetrics = RadarProcessingRouteMetrics.Empty;
        var payload = batch.Payload.Span;

        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            var streamEvent = events[eventIndex];
            var partition = topology.GetPartitionForSource(streamEvent.SourceId);
            var partitionId = partition.PartitionId;
            var shardId = partition.ShardId;
            var payloadMetrics = RadarProcessingPayloadReader.ComputeEventMetrics(streamEvent, payload);

            routedEvents[eventIndex] = new RadarProcessingRoutedEvent(
                eventIndex,
                streamEvent.SourceId,
                partitionId,
                shardId,
                payloadMetrics);
            partitionEventIndexes[partitionId][partitionOffsets[partitionId]++] = eventIndex;
            shardEventIndexes[shardId][shardOffsets[shardId]++] = eventIndex;
            partitionMetrics[partitionId] = partitionMetrics[partitionId].AddEvent(payloadMetrics);
            shardMetrics[shardId] = shardMetrics[shardId].AddEvent(payloadMetrics);
            totalMetrics = totalMetrics.AddEvent(payloadMetrics);
        }

        return new RadarProcessingBatchRoute(
            topology.Version,
            routedEvents,
            CreatePartitionRoutes(partitionEventIndexes, partitionMetrics),
            CreateShardRoutes(shardEventIndexes, shardMetrics),
            totalMetrics);
    }

    private static int[][] CreateEventIndexBuffers(int[] eventCounts)
    {
        var result = new int[eventCounts.Length][];
        for (var i = 0; i < eventCounts.Length; i++)
        {
            result[i] = eventCounts[i] == 0 ? Array.Empty<int>() : new int[eventCounts[i]];
        }

        return result;
    }

    private RadarProcessingPartitionBatchRoute[] CreatePartitionRoutes(
        int[][] eventIndexes,
        RadarProcessingRouteMetrics[] metrics)
    {
        var routes = new RadarProcessingPartitionBatchRoute[topology.PartitionCount];
        for (var partitionId = 0; partitionId < routes.Length; partitionId++)
        {
            var partition = topology.GetPartition(partitionId);
            routes[partitionId] = new RadarProcessingPartitionBatchRoute(
                partitionId,
                partition.ShardId,
                eventIndexes[partitionId],
                metrics[partitionId]);
        }

        return routes;
    }

    private RadarProcessingShardBatchRoute[] CreateShardRoutes(
        int[][] eventIndexes,
        RadarProcessingRouteMetrics[] metrics)
    {
        var routes = new RadarProcessingShardBatchRoute[topology.ShardCount];
        for (var shardId = 0; shardId < routes.Length; shardId++)
        {
            routes[shardId] = new RadarProcessingShardBatchRoute(
                shardId,
                eventIndexes[shardId],
                metrics[shardId]);
        }

        return routes;
    }
}
