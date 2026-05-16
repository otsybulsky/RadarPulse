using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingTopologyTests
{
    [Fact]
    public void TopologyCreatesContiguousSourceBlocks()
    {
        var topology = CreateTopology(sourceCount: 10, partitionCount: 3, shardCount: 2);

        Assert.Collection(
            topology.Partitions,
            first =>
            {
                Assert.Equal(0, first.PartitionId);
                Assert.Equal(0, first.ShardId);
                Assert.Equal(0, first.SourceIdStart);
                Assert.Equal(4, first.SourceIdEndExclusive);
            },
            second =>
            {
                Assert.Equal(1, second.PartitionId);
                Assert.Equal(0, second.ShardId);
                Assert.Equal(4, second.SourceIdStart);
                Assert.Equal(7, second.SourceIdEndExclusive);
            },
            third =>
            {
                Assert.Equal(2, third.PartitionId);
                Assert.Equal(1, third.ShardId);
                Assert.Equal(7, third.SourceIdStart);
                Assert.Equal(10, third.SourceIdEndExclusive);
            });
    }

    [Fact]
    public void TopologyMapsEverySourceIdToExactlyOnePartition()
    {
        var topology = CreateTopology(sourceCount: 10, partitionCount: 3, shardCount: 2);
        var expectedPartitionIds = new[] { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2 };

        for (var sourceId = 0; sourceId < topology.SourceCount; sourceId++)
        {
            var partitionId = topology.GetPartitionIdForSource(sourceId);
            var partition = topology.GetPartitionForSource(sourceId);

            Assert.Equal(expectedPartitionIds[sourceId], partitionId);
            Assert.Equal(partitionId, partition.PartitionId);
            Assert.True(partition.ContainsSourceId(sourceId));
        }
    }

    [Fact]
    public void TopologyPartitionsCoverEverySourceIdOnce()
    {
        var topology = CreateTopology(sourceCount: 17, partitionCount: 5, shardCount: 2);
        var visited = new bool[topology.SourceCount];
        var expectedSourceIdStart = 0;

        foreach (var partition in topology.Partitions)
        {
            Assert.Equal(expectedSourceIdStart, partition.SourceIdStart);
            Assert.True(partition.SourceCount > 0);

            for (var sourceId = partition.SourceIdStart;
                 sourceId < partition.SourceIdEndExclusive;
                 sourceId++)
            {
                Assert.False(visited[sourceId]);
                visited[sourceId] = true;
            }

            expectedSourceIdStart = partition.SourceIdEndExclusive;
        }

        Assert.Equal(topology.SourceCount, expectedSourceIdStart);
        Assert.All(visited, Assert.True);
    }

    [Fact]
    public void TopologyMapsPartitionsInsideShardRange()
    {
        var topology = CreateTopology(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var expectedShardIds = new[] { 0, 0, 1, 1, 2, 2 };

        for (var partitionId = 0; partitionId < topology.PartitionCount; partitionId++)
        {
            var shardId = topology.GetShardIdForPartition(partitionId);

            Assert.Equal(expectedShardIds[partitionId], shardId);
            Assert.InRange(shardId, 0, topology.ShardCount - 1);
        }

        Assert.Equal(0, topology.GetShardIdForSource(0));
        Assert.Equal(1, topology.GetShardIdForSource(4));
        Assert.Equal(2, topology.GetShardIdForSource(11));
    }

    [Fact]
    public void SameTopologyInputsProduceStableAssignments()
    {
        var first = CreateTopology(sourceCount: 31, partitionCount: 7, shardCount: 3);
        var second = CreateTopology(sourceCount: 31, partitionCount: 7, shardCount: 3);

        Assert.Equal(first.SourceUniverseVersion, second.SourceUniverseVersion);
        Assert.Equal(first.Partitions, second.Partitions);
    }

    [Fact]
    public void TopologyRejectsNullInputs()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var options = new RadarProcessingCoreOptions(partitionCount: 2, shardCount: 1);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingTopology(null!, options));
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingTopology(universe, null!));
    }

    [Fact]
    public void TopologyRejectsMorePartitionsThanSources()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var options = new RadarProcessingCoreOptions(partitionCount: 3, shardCount: 1);

        Assert.Throws<ArgumentException>(() => new RadarProcessingTopology(universe, options));
    }

    [Fact]
    public void TopologyRejectsSourceAndPartitionIdsOutsideRange()
    {
        var topology = CreateTopology(sourceCount: 4, partitionCount: 2, shardCount: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetPartitionIdForSource(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetPartitionIdForSource(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetPartition(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetPartition(2));
    }

    [Fact]
    public void PartitionAssignmentRejectsInvalidRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPartitionAssignment(-1, shardId: 0, sourceIdStart: 0, sourceIdEndExclusive: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPartitionAssignment(partitionId: 0, shardId: -1, sourceIdStart: 0, sourceIdEndExclusive: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPartitionAssignment(partitionId: 0, shardId: 0, sourceIdStart: -1, sourceIdEndExclusive: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPartitionAssignment(partitionId: 0, shardId: 0, sourceIdStart: 1, sourceIdEndExclusive: 1));
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
}
