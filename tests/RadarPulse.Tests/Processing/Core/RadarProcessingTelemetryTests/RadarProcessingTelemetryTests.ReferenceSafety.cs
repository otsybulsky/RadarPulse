using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingTelemetryTests
{
    [Fact]
    public void PartitionedTelemetryDoesNotRetainLeasedPayloadReferences()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 4);
        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 100, payload: new byte[] { 1, 2, 3, 4 });
        RadarProcessingTelemetry? capturedTelemetry = null;

        builder.ConsumeLeased(batch =>
        {
            var result = core.Process(batch);
            capturedTelemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);
        });

        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 101, payload: new byte[] { 100, 101, 102, 103 });

        Assert.NotNull(capturedTelemetry);
        Assert.Equal(1, capturedTelemetry.BatchMetrics.EventCount);
        Assert.Equal(4, capturedTelemetry.BatchMetrics.PayloadValueCount);
        Assert.Equal(10, capturedTelemetry.BatchMetrics.RawValueChecksum);
        Assert.Equal(10, capturedTelemetry.Partitions[0].RawValueChecksum);
        Assert.Equal(10, capturedTelemetry.Shards[0].RawValueChecksum);
    }

    [Fact]
    public void SequentialResultDoesNotCarryPartitionedTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateEightBitBatch(universe.Version, new[] { 0 });

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.Sequential, result.ExecutionMode);
        Assert.Equal(core.Topology.Version, result.TopologyVersion);
        Assert.Null(result.Telemetry);
    }
}
