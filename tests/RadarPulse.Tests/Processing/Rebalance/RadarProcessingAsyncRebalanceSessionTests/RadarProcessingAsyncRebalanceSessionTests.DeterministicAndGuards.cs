using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncRebalanceSessionTests
{
    [Fact]
    public async Task SyncAndAsyncRebalanceProduceMatchingDeterministicState()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var syncSession = CreateSyncSession(universe);
        await using var asyncSession = CreateAsyncSession(universe);
        var batch = CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]);

        var syncResult = syncSession.Process(batch);
        var asyncResult = await asyncSession.ProcessAsync(batch);

        Assert.True(syncResult.ProcessingResult.IsValid);
        Assert.True(asyncResult.ProcessingResult.IsValid);
        Assert.Equal(syncResult.ProcessingResult.Metrics, asyncResult.ProcessingResult.Metrics);
        Assert.Equal(syncSession.Core.CreateSourceSnapshots(), asyncSession.Core.CreateSourceSnapshots());
        Assert.Equal(syncResult.PressureSample!.BatchMetrics, asyncResult.PressureSample!.BatchMetrics);
        Assert.Equal(syncResult.RebalanceDecision!.Kind, asyncResult.RebalanceDecision!.Kind);
        Assert.Equal(syncResult.RebalanceDecision.MoveKind, asyncResult.RebalanceDecision.MoveKind);
        Assert.Equal(syncResult.RebalanceDecision.PartitionId, asyncResult.RebalanceDecision.PartitionId);
        Assert.Equal(syncResult.PublishedMigration, asyncResult.PublishedMigration);
        Assert.Equal(syncSession.CurrentTopology.Version, asyncSession.CurrentTopology.Version);
        for (var partitionId = 0; partitionId < universe.SourceCount; partitionId++)
        {
            Assert.Equal(
                syncSession.CurrentTopology.GetShardIdForPartition(partitionId),
                asyncSession.CurrentTopology.GetShardIdForPartition(partitionId));
        }
    }

    [Fact]
    public void SyncRebalanceProcessRejectsAsyncCore()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 1,
            shardCount: 1);
        var session = new RadarProcessingRebalanceSession(core);

        var exception = Assert.Throws<NotSupportedException>(() => session.Process(CreateEmptyBatch(universe.Version)));

        Assert.Contains("RadarProcessingAsyncRebalanceSession.ProcessAsync", exception.Message, StringComparison.Ordinal);
    }
}
