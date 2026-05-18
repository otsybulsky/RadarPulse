using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPressureSkewTransformerTests
{
    [Fact]
    public void NoneProfileReturnsOriginalSample()
    {
        var sample = CreateSample();
        var transformer = new RadarProcessingPressureSkewTransformer();

        var result = transformer.Apply(sample, evaluationSequence: 1);

        Assert.Same(sample, result);
    }

    [Fact]
    public void HotShardCreatesMovableSyntheticPressureWithoutChangingShape()
    {
        var sample = CreateSample();
        var transformer = new RadarProcessingPressureSkewTransformer(
            new RadarProcessingPressureSkewOptions(RadarProcessingPressureSkewProfile.HotShard));

        var result = transformer.Apply(sample, evaluationSequence: 1);

        Assert.NotSame(sample, result);
        Assert.Equal(sample.TopologyVersion, result.TopologyVersion);
        Assert.Equal(sample.BatchMetrics, result.BatchMetrics);
        Assert.Equal(sample.PartitionCount, result.PartitionCount);
        Assert.Equal(sample.ShardCount, result.ShardCount);
        Assert.True(result.Shards[0].Score.Value >= 50_000.0);
        Assert.Equal(RadarProcessingPressureBand.Hot, result.Shards[0].Band);
        Assert.All(
            result.Partitions.Where(partition => partition.ShardId == 0),
            partition =>
            {
                Assert.True(partition.Score.Value > 0.0);
                Assert.True(partition.Score.Value < 10_000.0);
            });
    }

    [Fact]
    public void RotatingHotShardHonorsPeriod()
    {
        var sample = CreateSample();
        var transformer = new RadarProcessingPressureSkewTransformer(
            new RadarProcessingPressureSkewOptions(
                RadarProcessingPressureSkewProfile.RotatingHotShard,
                period: 2));

        var first = transformer.Apply(sample, evaluationSequence: 1);
        var second = transformer.Apply(sample, evaluationSequence: 3);

        Assert.Equal(RadarProcessingPressureBand.Hot, first.Shards[0].Band);
        Assert.Equal(RadarProcessingPressureBand.Cold, first.Shards[1].Band);
        Assert.Equal(RadarProcessingPressureBand.Cold, second.Shards[0].Band);
        Assert.Equal(RadarProcessingPressureBand.Hot, second.Shards[1].Band);
    }

    [Fact]
    public void TargetStarvationLeavesNoColdTargets()
    {
        var sample = CreateSample();
        var transformer = new RadarProcessingPressureSkewTransformer(
            new RadarProcessingPressureSkewOptions(RadarProcessingPressureSkewProfile.TargetStarvation));

        var result = transformer.Apply(sample, evaluationSequence: 1);

        Assert.DoesNotContain(result.Shards, shard => shard.Band == RadarProcessingPressureBand.Cold);
    }

    [Fact]
    public void OptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureSkewOptions((RadarProcessingPressureSkewProfile)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureSkewOptions(factor: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPressureSkewOptions(period: 0));
    }

    private static RadarProcessingPressureSample CreateSample()
    {
        const int shardCount = 4;
        const int partitionCount = 24;
        var shards = new RadarProcessingShardPressureSample[shardCount];
        for (var shardId = 0; shardId < shardCount; shardId++)
        {
            shards[shardId] = new RadarProcessingShardPressureSample(
                shardId,
                partitionCount: 6,
                activePartitionCount: 0,
                RadarProcessingRouteMetrics.Empty,
                new RadarProcessingPressureScore(0),
                RadarProcessingPressureBand.Cold);
        }

        var partitions = new RadarProcessingPartitionPressureSample[partitionCount];
        for (var partitionId = 0; partitionId < partitionCount; partitionId++)
        {
            partitions[partitionId] = new RadarProcessingPartitionPressureSample(
                partitionId,
                partitionId / 6,
                RadarProcessingRouteMetrics.Empty,
                new RadarProcessingPressureScore(0),
                RadarProcessingPressureBand.Cold);
        }

        return RadarProcessingPressureSample.Create(
            new RadarProcessingTopologyVersion(1),
            RadarProcessingRouteMetrics.Empty,
            shards,
            partitions);
    }
}
