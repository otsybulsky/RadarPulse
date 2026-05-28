using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingTelemetryTests
{
    [Fact]
    public void EmptyPartitionedBatchTelemetryIsStable()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var batch = CreateBatch(
            universe.Version,
            Array.Empty<RadarStreamEvent>(),
            Array.Empty<byte>());

        var result = core.Process(batch);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(result.Telemetry);

        Assert.True(result.IsValid);
        Assert.Equal(result.TopologyVersion, telemetry.TopologyVersion);
        Assert.Equal(RadarProcessingRouteMetrics.Empty, telemetry.BatchMetrics);
        Assert.Equal(-1, telemetry.HotPartitionId);
        Assert.Equal(-1, telemetry.HotShardId);

        foreach (var partition in telemetry.Partitions)
        {
            Assert.False(partition.HasWork);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, partition.Metrics);
        }

        foreach (var shard in telemetry.Shards)
        {
            Assert.False(shard.HasWork);
            Assert.Equal(RadarProcessingRouteMetrics.Empty, shard.Metrics);
            Assert.Equal(0, shard.ActivePartitionCount);
        }
    }

    [Fact]
    public void TelemetryReportsHotShardAndPartitionDeterministically()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var hotBatch = CreateEightBitBatch(universe.Version, new[] { 2, 2, 0 });

        var hotTelemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(hotBatch).Telemetry);

        Assert.Equal(2, hotTelemetry.HotPartitionId);
        Assert.Equal(1, hotTelemetry.HotShardId);

        var tieCore = CreatePartitionedCore(universe, partitionCount: 4, shardCount: 2);
        var tieBatch = CreateEightBitBatch(universe.Version, new[] { 0, 2 });

        var tieTelemetry = Assert.IsType<RadarProcessingTelemetry>(tieCore.Process(tieBatch).Telemetry);

        Assert.Equal(0, tieTelemetry.HotPartitionId);
        Assert.Equal(0, tieTelemetry.HotShardId);
    }
}
