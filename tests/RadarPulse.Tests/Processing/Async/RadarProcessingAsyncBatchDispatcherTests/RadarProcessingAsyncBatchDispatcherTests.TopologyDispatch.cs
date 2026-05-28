using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchDispatcherTests
{
    [Fact]
    public async Task DispatchUsesOneCapturedTopologyVersionEvenIfTopologyMovesDuringExecution()
    {
        var manager = CreateTopologyManager(sourceCount: 6, partitionCount: 6, shardCount: 3);
        await using var group = CreateStartedGroup(workerCount: 3, queueCapacity: 1);
        var providerCallCount = 0;
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(
            group,
            () =>
            {
                providerCallCount++;
                return manager.Current;
        });
        var batch = CreateEightBitBatch(manager.Current.SourceUniverseVersion, sourceIds: [1]);
        var capturedVersion = manager.Current.Version;
        var moved = 0;

        var result = await dispatcher.DispatchAsync(
            batchSequence: 1,
            batch,
            (borrowedBatch, route, workItem, _) =>
            {
                if (Interlocked.CompareExchange(ref moved, 1, 0) == 0)
                {
                    var move = manager.MovePartition(
                        new RadarProcessingTopologyMoveRequest(
                            capturedVersion,
                            partitionId: 1,
                            sourceShardId: 0,
                            targetShardId: 2));
                    Assert.True(move.Succeeded);
                }

                Assert.Equal(capturedVersion, route.TopologyVersion);
                Assert.Equal(capturedVersion, workItem.TopologyVersion);
                Assert.Equal(0, route.GetRoutedEvent(0).ShardId);
                return Succeed(workItem, CancellationToken.None);
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Volatile.Read(ref moved));
        Assert.Equal(1, providerCallCount);
        Assert.Equal(capturedVersion, result.TopologyVersion);
        Assert.Equal(capturedVersion.Next(), manager.Current.Version);
    }
}
