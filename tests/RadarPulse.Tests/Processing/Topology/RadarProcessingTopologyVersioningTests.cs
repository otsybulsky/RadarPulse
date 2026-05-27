using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingTopologyVersioningTests
{
    [Fact]
    public void TopologyStartsAtInitialVersion()
    {
        var topology = CreateTopology(sourceCount: 12, partitionCount: 6, shardCount: 3);

        Assert.Equal(RadarProcessingTopologyVersion.Initial, topology.Version);
    }

    [Fact]
    public void TopologyManagerPublishesNewVersionForValidMove()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var previous = manager.Current;
        var request = new RadarProcessingTopologyMoveRequest(
            previous.Version,
            partitionId: 1,
            sourceShardId: 0,
            targetShardId: 2);

        var result = manager.MovePartition(request);

        Assert.True(result.Succeeded);
        Assert.Equal(RadarProcessingTopologyMoveError.None, result.Error);
        Assert.Same(previous, result.PreviousTopology);
        Assert.Same(manager.Current, result.CurrentTopology);
        Assert.Equal(previous.Version.Next(), manager.Current.Version);
        Assert.Equal(2, manager.Current.GetShardIdForPartition(1));
    }

    [Fact]
    public void TopologyManagerDoesNotMutatePreviousSnapshot()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var previous = manager.Current;

        var result = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                previous.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));

        Assert.True(result.Succeeded);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, previous.Version);
        Assert.Equal(0, previous.GetShardIdForPartition(1));
        Assert.Equal(2, manager.Current.GetShardIdForPartition(1));
    }

    [Fact]
    public void PartitionMovePreservesSourceToPartitionMapping()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var previous = manager.Current;
        var sourcePartitionIds = new int[previous.SourceCount];

        for (var sourceId = 0; sourceId < previous.SourceCount; sourceId++)
        {
            sourcePartitionIds[sourceId] = previous.GetPartitionIdForSource(sourceId);
        }

        var result = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                previous.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));

        Assert.True(result.Succeeded);
        for (var sourceId = 0; sourceId < manager.Current.SourceCount; sourceId++)
        {
            Assert.Equal(sourcePartitionIds[sourceId], manager.Current.GetPartitionIdForSource(sourceId));
        }
    }

    [Fact]
    public void PartitionMoveChangesOnlyRequestedPartitionOwner()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var previous = manager.Current;

        var result = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                previous.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));

        Assert.True(result.Succeeded);
        for (var partitionId = 0; partitionId < previous.PartitionCount; partitionId++)
        {
            var expectedShardId = partitionId == 1
                ? 2
                : previous.GetShardIdForPartition(partitionId);
            Assert.Equal(expectedShardId, manager.Current.GetShardIdForPartition(partitionId));
        }
    }

    [Fact]
    public void TopologyManagerRejectsStaleMoveRequest()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var initial = manager.Current;
        var firstMove = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                initial.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));

        var staleMove = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                initial.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));

        Assert.True(firstMove.Succeeded);
        Assert.False(staleMove.Succeeded);
        Assert.Equal(RadarProcessingTopologyMoveError.StaleTopologyVersion, staleMove.Error);
        Assert.Same(manager.Current, staleMove.CurrentTopology);
        Assert.Equal(firstMove.CurrentTopology.Version, manager.Current.Version);
    }

    [Theory]
    [InlineData(-1, 0, 1, RadarProcessingTopologyMoveError.PartitionIdOutOfRange)]
    [InlineData(6, 0, 1, RadarProcessingTopologyMoveError.PartitionIdOutOfRange)]
    [InlineData(0, -1, 1, RadarProcessingTopologyMoveError.SourceShardIdOutOfRange)]
    [InlineData(0, 3, 1, RadarProcessingTopologyMoveError.SourceShardIdOutOfRange)]
    [InlineData(0, 0, -1, RadarProcessingTopologyMoveError.TargetShardIdOutOfRange)]
    [InlineData(0, 0, 3, RadarProcessingTopologyMoveError.TargetShardIdOutOfRange)]
    [InlineData(0, 0, 0, RadarProcessingTopologyMoveError.NoOpMove)]
    [InlineData(0, 1, 2, RadarProcessingTopologyMoveError.SourceShardOwnershipMismatch)]
    public void TopologyManagerRejectsInvalidMoveRequests(
        int partitionId,
        int sourceShardId,
        int targetShardId,
        RadarProcessingTopologyMoveError expectedError)
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var current = manager.Current;

        var result = manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                current.Version,
                partitionId,
                sourceShardId,
                targetShardId));

        Assert.False(result.Succeeded);
        Assert.Equal(expectedError, result.Error);
        Assert.Same(current, manager.Current);
        Assert.Same(current, result.CurrentTopology);
        Assert.Same(current, result.PreviousTopology);
    }

    [Fact]
    public void TopologyVersionRejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingTopologyVersion(-1));
    }

    private static RadarProcessingTopology CreateTopology(
        int sourceCount,
        int partitionCount,
        int shardCount) =>
        new(
            CreateUniverse(sourceCount),
            CreateOptions(partitionCount, shardCount));

    private static RadarProcessingTopologyManager CreateManager(
        int sourceCount,
        int partitionCount,
        int shardCount) =>
        new(
            CreateUniverse(sourceCount),
            CreateOptions(partitionCount, shardCount));

    private static RadarProcessingCoreOptions CreateOptions(
        int partitionCount,
        int shardCount) =>
        new(
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount,
            shardCount);

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
}
