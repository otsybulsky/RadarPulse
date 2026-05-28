using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingBatchRouterTests
{
    [Fact]
    public void RouteCoversEveryEventExactlyOnceAcrossPartitions()
    {
        var topology = CreateTopology(sourceCount: 5, partitionCount: 3, shardCount: 2);
        var router = new RadarProcessingBatchRouter(topology);
        var batch = CreateEightBitBatch(
            topology.SourceUniverseVersion,
            sourceIds: [0, 2, 4, 1, 3]);

        var route = router.Route(batch);
        var visited = new bool[batch.EventCount];

        foreach (var partition in route.Partitions)
        {
            foreach (var eventIndex in partition.EventIndexes.ToArray())
            {
                Assert.InRange(eventIndex, 0, batch.EventCount - 1);
                Assert.False(visited[eventIndex]);
                visited[eventIndex] = true;
            }
        }

        Assert.All(visited, Assert.True);
    }

    [Fact]
    public void RouteAssignmentsMatchTopology()
    {
        var topology = CreateTopology(sourceCount: 5, partitionCount: 3, shardCount: 2);
        var router = new RadarProcessingBatchRouter(topology);
        var batch = CreateEightBitBatch(
            topology.SourceUniverseVersion,
            sourceIds: [0, 2, 4, 1, 3]);

        var route = router.Route(batch);

        foreach (var routedEvent in route.RoutedEvents.ToArray())
        {
            Assert.Equal(routedEvent.EventIndex, route.GetRoutedEvent(routedEvent.EventIndex).EventIndex);
            Assert.Equal(topology.GetPartitionIdForSource(routedEvent.SourceId), routedEvent.PartitionId);
            Assert.Equal(topology.GetShardIdForSource(routedEvent.SourceId), routedEvent.ShardId);
            Assert.Contains(routedEvent.EventIndex, route.GetPartition(routedEvent.PartitionId).EventIndexes.ToArray());
            Assert.Contains(routedEvent.EventIndex, route.GetShard(routedEvent.ShardId).EventIndexes.ToArray());
        }
    }

    [Fact]
    public void SameSourceEventsPreserveBatchOrderInsidePartitionAndShardRoutes()
    {
        var topology = CreateTopology(sourceCount: 4, partitionCount: 2, shardCount: 2);
        var router = new RadarProcessingBatchRouter(topology);
        var batch = CreateEightBitBatch(
            topology.SourceUniverseVersion,
            sourceIds: [1, 3, 1, 3, 1]);
        var sourceOnePartitionId = topology.GetPartitionIdForSource(sourceId: 1);
        var sourceOneShardId = topology.GetShardIdForSource(sourceId: 1);

        var route = router.Route(batch);
        var sourceOnePartitionIndexes = FilterSourceIndexes(
            route.GetPartition(sourceOnePartitionId).EventIndexes.ToArray(),
            batch,
            sourceId: 1);
        var sourceOneShardIndexes = FilterSourceIndexes(
            route.GetShard(sourceOneShardId).EventIndexes.ToArray(),
            batch,
            sourceId: 1);

        Assert.Equal(new[] { 0, 2, 4 }, sourceOnePartitionIndexes);
        Assert.Equal(new[] { 0, 2, 4 }, sourceOneShardIndexes);
    }
}
