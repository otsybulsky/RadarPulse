using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingTelemetryTests
{
    [Fact]
    public void PartitionedBarrierResultCarriesShardTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var events = new[]
        {
            CreateEvent(
                sourceId: 0,
                messageTimestampUtcTicks: 100,
                payloadOffset: 0,
                gateCount: 2,
                wordSize: RadarStreamWordSize.EightBit),
            CreateEvent(
                sourceId: 2,
                messageTimestampUtcTicks: 101,
                payloadOffset: 2,
                gateCount: 1,
                wordSize: RadarStreamWordSize.SixteenBit),
            CreateEvent(
                sourceId: 3,
                messageTimestampUtcTicks: 102,
                payloadOffset: 4,
                gateCount: 3,
                wordSize: RadarStreamWordSize.EightBit)
        };
        var payload = new byte[] { 1, 2, 0, 5, 6, 7, 8 };
        var batch = CreateBatch(universe.Version, events, payload);
        var batchMetrics = RadarEventBatchMetrics.Compute(batch);

        var result = core.Process(batch);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);

        Assert.True(result.IsValid);
        Assert.Equal(core.Topology.Version, result.TopologyVersion);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, telemetry.ExecutionMode);
        Assert.Equal(result.TopologyVersion, telemetry.TopologyVersion);
        Assert.Equal(4, telemetry.PartitionCount);
        Assert.Equal(2, telemetry.ShardCount);
        Assert.Equal(batch.EventCount, telemetry.BatchMetrics.EventCount);
        Assert.Equal(batchMetrics.PayloadValueCount, telemetry.BatchMetrics.PayloadValueCount);
        Assert.Equal(batchMetrics.RawValueChecksum, telemetry.BatchMetrics.RawValueChecksum);
        Assert.Equal(telemetry.BatchMetrics, SumPartitionMetrics(telemetry));
        Assert.Equal(telemetry.BatchMetrics, SumShardMetrics(telemetry));
        Assert.Equal(2, telemetry.Shards[0].PartitionCount);
        Assert.Equal(1, telemetry.Shards[0].ActivePartitionCount);
        Assert.Equal(2, telemetry.Shards[1].PartitionCount);
        Assert.Equal(2, telemetry.Shards[1].ActivePartitionCount);
    }
}
