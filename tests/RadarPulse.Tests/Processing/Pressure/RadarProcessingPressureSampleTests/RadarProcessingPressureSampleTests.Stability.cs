using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPressureSampleTests
{
    [Fact]
    public void PressureSampleDoesNotRetainLeasedPayloadReferences()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreatePartitionedCore(universe, partitionCount: 1, shardCount: 1);
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 4);
        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 100, payload: [1, 2, 3, 4]);
        RadarProcessingPressureSample? capturedSample = null;

        builder.ConsumeLeased(batch =>
        {
            var result = core.Process(batch);
            var telemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);
            capturedSample = RadarProcessingPressureSample.FromTelemetry(telemetry);
        });

        AddEvent(builder, sourceId: 0, messageTimestampUtcTicks: 101, payload: [100, 101, 102, 103]);

        Assert.NotNull(capturedSample);
        Assert.Equal(1, capturedSample.BatchMetrics.EventCount);
        Assert.Equal(4, capturedSample.BatchMetrics.PayloadValueCount);
        Assert.Equal(10, capturedSample.BatchMetrics.RawValueChecksum);
        Assert.Equal(10, capturedSample.Partitions[0].RawValueChecksum);
        Assert.Equal(10, capturedSample.Shards[0].RawValueChecksum);
    }

    [Fact]
    public void PressureSampleRemainsStableAfterFurtherProcessing()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreatePartitionedCore(universe, partitionCount: 2, shardCount: 1);
        var firstTelemetry = Assert.IsType<RadarProcessingTelemetry>(
            core.Process(CreateEightBitBatch(universe.Version, sourceIds: [0])).Telemetry);
        var sample = RadarProcessingPressureSample.FromTelemetry(firstTelemetry);

        core.Process(CreateEightBitBatch(universe.Version, sourceIds: [0, 1]));

        Assert.Equal(firstTelemetry.TopologyVersion, sample.TopologyVersion);
        Assert.Equal(firstTelemetry.BatchMetrics, sample.BatchMetrics);
        Assert.Equal(firstTelemetry.Shards[0].Metrics, sample.Shards[0].Metrics);
        Assert.Equal(firstTelemetry.Partitions[0].Metrics, sample.Partitions[0].Metrics);
        Assert.Equal(firstTelemetry.Partitions[1].Metrics, sample.Partitions[1].Metrics);
    }
}
