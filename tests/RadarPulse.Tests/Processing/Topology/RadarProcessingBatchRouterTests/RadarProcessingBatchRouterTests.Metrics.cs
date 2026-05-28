using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingBatchRouterTests
{
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
}
