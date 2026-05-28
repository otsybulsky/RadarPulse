using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingBatchRouterTests
{
    [Fact]
    public void RouteEmptyBatchCreatesStableEmptyRoute()
    {
        var topology = CreateTopology(sourceCount: 5, partitionCount: 3, shardCount: 2);
        var router = new RadarProcessingBatchRouter(topology);
        var batch = CreateBatch(
            topology.SourceUniverseVersion,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var route = router.Route(batch);

        Assert.Equal(0, route.EventCount);
        Assert.Equal(topology.Version, route.TopologyVersion);
        Assert.Equal(3, route.PartitionCount);
        Assert.Equal(2, route.ShardCount);
        Assert.Equal(RadarProcessingRouteMetrics.Empty, route.Metrics);
        Assert.Empty(route.RoutedEvents.ToArray());

        foreach (var partition in route.Partitions)
        {
            Assert.False(partition.HasWork);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, partition.Metrics);
            Assert.Empty(partition.EventIndexes.ToArray());
        }

        foreach (var shard in route.Shards)
        {
            Assert.False(shard.HasWork);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, shard.Metrics);
            Assert.Empty(shard.EventIndexes.ToArray());
        }
    }

    [Fact]
    public void RouteCapturesTopologyVersion()
    {
        var manager = CreateTopologyManager(sourceCount: 6, partitionCount: 6, shardCount: 3);
        var firstTopology = manager.Current;
        var firstRouter = new RadarProcessingBatchRouter(firstTopology);
        var batch = CreateEightBitBatch(
            firstTopology.SourceUniverseVersion,
            sourceIds: [1]);

        var firstRoute = firstRouter.Route(batch);
        var move = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                firstTopology.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));
        var secondRoute = new RadarProcessingBatchRouter(manager.Current).Route(batch);

        Assert.True(move.Succeeded);
        Assert.Equal(firstTopology.Version, firstRoute.TopologyVersion);
        Assert.Equal(firstTopology.Version.Next(), secondRoute.TopologyVersion);
        Assert.Equal(0, firstRoute.GetRoutedEvent(0).ShardId);
        Assert.Equal(2, secondRoute.GetRoutedEvent(0).ShardId);
    }
}
