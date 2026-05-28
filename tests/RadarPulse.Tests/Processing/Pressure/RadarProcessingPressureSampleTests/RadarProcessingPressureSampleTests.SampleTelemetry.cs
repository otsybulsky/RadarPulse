using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPressureSampleTests
{
    [Fact]
    public void EmptyTelemetryProducesZeroPressure()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        var sample = RadarProcessingPressureSample.FromTelemetry(telemetry);

        Assert.Equal(telemetry.TopologyVersion, sample.TopologyVersion);
        Assert.Equal(RadarProcessingRouteMetrics.Empty, sample.BatchMetrics);
        Assert.All(sample.Shards, shard =>
        {
            Assert.Equal(RadarProcessingPressureScore.Zero, shard.Score);
            Assert.Equal(RadarProcessingPressureBand.Cold, shard.Band);
        });
        Assert.All(sample.Partitions, partition =>
        {
            Assert.Equal(RadarProcessingPressureScore.Zero, partition.Score);
            Assert.Equal(RadarProcessingPressureBand.Cold, partition.Band);
        });
    }

    [Fact]
    public void PressureSampleCopiesTopologyVersionAndTelemetryMetrics()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateEightBitBatch(universe.Version, sourceIds: [0, 2, 2, 3]);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        var sample = RadarProcessingPressureSample.FromTelemetry(telemetry);

        Assert.Equal(telemetry.TopologyVersion, sample.TopologyVersion);
        Assert.Equal(telemetry.BatchMetrics, sample.BatchMetrics);
        Assert.Equal(telemetry.ShardCount, sample.ShardCount);
        Assert.Equal(telemetry.PartitionCount, sample.PartitionCount);

        for (var shardId = 0; shardId < telemetry.ShardCount; shardId++)
        {
            Assert.Equal(telemetry.Shards[shardId].ShardId, sample.Shards[shardId].ShardId);
            Assert.Equal(telemetry.Shards[shardId].Metrics, sample.Shards[shardId].Metrics);
            Assert.Equal(
                telemetry.Shards[shardId].ActivePartitionCount,
                sample.Shards[shardId].ActivePartitionCount);
        }

        for (var partitionId = 0; partitionId < telemetry.PartitionCount; partitionId++)
        {
            Assert.Equal(telemetry.Partitions[partitionId].PartitionId, sample.Partitions[partitionId].PartitionId);
            Assert.Equal(telemetry.Partitions[partitionId].ShardId, sample.Partitions[partitionId].ShardId);
            Assert.Equal(telemetry.Partitions[partitionId].Metrics, sample.Partitions[partitionId].Metrics);
        }
    }
}
