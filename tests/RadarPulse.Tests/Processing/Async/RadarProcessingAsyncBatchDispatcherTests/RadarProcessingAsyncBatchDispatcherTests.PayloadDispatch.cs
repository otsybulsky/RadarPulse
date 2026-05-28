using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchDispatcherTests
{
    [Fact]
    public async Task DispatchPassesBorrowedBatchAndRouteToExecutorWithoutPayloadCopy()
    {
        await using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 3]);
        var observedExecutorCalls = 0;
        RadarProcessingBatchRoute? observedRoute = null;

        var result = await dispatcher.DispatchAsync(
            batchSequence: 2,
            batch,
            (borrowedBatch, route, workItem, cancellationToken) =>
            {
                Assert.Same(batch, borrowedBatch);
                observedRoute ??= route;
                Assert.Same(observedRoute, route);
                Assert.Equal(2, borrowedBatch.Payload.Length);
                Assert.Equal(topology.Version, route.TopologyVersion);
                Interlocked.Increment(ref observedExecutorCalls);
                return Succeed(workItem, cancellationToken);
            });

        Assert.True(result.IsSuccess);
        Assert.Same(observedRoute, result.Route);
        Assert.Equal(topology.ShardCount, observedExecutorCalls);
        Assert.True(result.DrainResult.IsDrained);
    }
}
