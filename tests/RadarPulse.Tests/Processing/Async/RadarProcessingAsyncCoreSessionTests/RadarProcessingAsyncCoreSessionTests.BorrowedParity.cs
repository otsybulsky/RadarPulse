using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCoreSessionTests
{
    [Fact]
    public async Task AsyncCoreSessionPreservesOwnedAndLeasedBorrowedBatchParity()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var ownedCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var leasedCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var ownedBatch = CreateMixedBatchBuilder(universe.Version).Build();

        await using var ownedSession = new RadarProcessingAsyncCoreSession(ownedCore);
        await using var leasedSession = new RadarProcessingAsyncCoreSession(leasedCore);
        var ownedResult = await ownedSession.ProcessAsync(ownedBatch);
        RadarProcessingResult? leasedResult = null;
        CreateMixedBatchBuilder(universe.Version).ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            leasedResult = leasedSession.ProcessAsync(batch).AsTask().GetAwaiter().GetResult();
        });

        Assert.NotNull(leasedResult);
        Assert.Equal(ownedResult.Metrics, leasedResult.Metrics);
        Assert.Equal(ownedCore.CreateSourceSnapshots(), leasedCore.CreateSourceSnapshots());
    }
}
