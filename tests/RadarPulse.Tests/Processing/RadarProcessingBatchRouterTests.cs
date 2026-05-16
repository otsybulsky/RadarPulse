using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingBatchRouterTests
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

    [Fact]
    public void RouteMetricsMatchBatchPayloadMetricsAndAggregateThroughPartitionsAndShards()
    {
        var topology = CreateTopology(sourceCount: 4, partitionCount: 2, shardCount: 2);
        var router = new RadarProcessingBatchRouter(topology);
        var events = new[]
        {
            CreateEvent(
                sourceId: 0,
                payloadOffset: 0,
                gateCount: 2,
                wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(
                sourceId: 2,
                payloadOffset: 2,
                gateCount: 2,
                wordSize: RadarStreamWordSize.SixteenBit),
            CreateEvent(
                sourceId: 3,
                payloadOffset: 6,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit)
        };
        byte[] payload = [1, 2, 0, 5, 1, 0, 7];
        var batch = CreateBatch(topology.SourceUniverseVersion, events, payload);
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        var route = router.Route(batch);

        Assert.Equal(batch.EventCount, route.Metrics.EventCount);
        Assert.Equal(batchMetrics.PayloadValueCount, route.Metrics.PayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, route.Metrics.RawValueChecksum);
        Assert.Equal(route.Metrics, SumPartitionMetrics(route));
        Assert.Equal(route.Metrics, SumShardMetrics(route));
    }

    [Fact]
    public void RouteMetricsRemainStableAfterOriginalPayloadArrayMutation()
    {
        var topology = CreateTopology(sourceCount: 1, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var streamEvent = CreateEvent(
            sourceId: 0,
            payloadOffset: 0,
            gateCount: 4,
            wordSize: RadarStreamWordSize.EightBit);
        byte[] payload = [1, 2, 3, 4];
        var batch = CreateBatch(topology.SourceUniverseVersion, [streamEvent], payload);

        var route = router.Route(batch);
        payload[0] = 100;

        Assert.Equal(10, route.Metrics.RawValueChecksum);
        Assert.Equal(new[] { 0 }, route.GetPartition(partitionId: 0).EventIndexes.ToArray());
        Assert.Equal(new[] { 0 }, route.GetShard(shardId: 0).EventIndexes.ToArray());
    }

    [Fact]
    public void RouterRejectsNullInputsAndSourceUniverseVersionMismatch()
    {
        var topology = CreateTopology(sourceCount: 2, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var mismatchedBatch = CreateEightBitBatch(
            new SourceUniverseVersion(2),
            sourceIds: [0]);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingBatchRouter(null!));
        Assert.Throws<ArgumentNullException>(() => router.Route(null!));
        Assert.Throws<ArgumentException>(() => router.Route(mismatchedBatch));
    }

    [Fact]
    public void RouterRejectsSourceIdOutsideTopologyBeforeReturningRoute()
    {
        var topology = CreateTopology(sourceCount: 2, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var batch = CreateEightBitBatch(
            topology.SourceUniverseVersion,
            sourceIds: [0, 2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => router.Route(batch));
    }

    [Fact]
    public void RouteRejectsInvalidLookupIds()
    {
        var topology = CreateTopology(sourceCount: 2, partitionCount: 1, shardCount: 1);
        var router = new RadarProcessingBatchRouter(topology);
        var route = router.Route(CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0]));

        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetRoutedEvent(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetRoutedEvent(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetPartition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetPartition(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetShard(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => route.GetShard(1));
    }

    private static RadarProcessingRouteMetrics SumPartitionMetrics(RadarProcessingBatchRoute route)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var partition in route.Partitions)
        {
            metrics = metrics.Add(partition.Metrics);
        }

        return metrics;
    }

    private static RadarProcessingRouteMetrics SumShardMetrics(RadarProcessingBatchRoute route)
    {
        var metrics = RadarProcessingRouteMetrics.Empty;
        foreach (var shard in route.Shards)
        {
            metrics = metrics.Add(shard.Metrics);
        }

        return metrics;
    }

    private static int[] FilterSourceIndexes(
        int[] eventIndexes,
        RadarEventBatch batch,
        int sourceId)
    {
        var result = new List<int>();
        foreach (var eventIndex in eventIndexes)
        {
            if (batch.Events.Span[eventIndex].SourceId == sourceId)
            {
                result.Add(eventIndex);
            }
        }

        return result.ToArray();
    }

    private static RadarProcessingTopology CreateTopology(
        int sourceCount,
        int partitionCount,
        int shardCount) =>
        new(
            CreateUniverse(sourceCount),
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];
        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(
                sourceIds[i],
                payloadOffset: i,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit);
            payload[i] = (byte)(i + 1);
        }

        return CreateBatch(sourceUniverseVersion, events, payload);
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        RadarStreamEvent[] events,
        byte[] payload) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        int payloadOffset,
        ushort gateCount,
        RadarStreamWordSize wordSize)
    {
        var payloadLength = checked(gateCount * (wordSize == RadarStreamWordSize.EightBit ? 1 : 2));
        return new RadarStreamEvent(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100 + payloadOffset,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: payloadOffset,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: gateCount,
            wordSize: wordSize,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: payloadLength);
    }
}
