using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSessionTests
{
    [Fact]
    public async Task QueuedRebalanceSessionRejectsInvalidAsyncComposition()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var syncRebalance = CreateSession(universe, shardCount: 1);
        var asyncRebalance = CreateSession(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            shardCount: 1);
        var otherAsyncRebalance = CreateSession(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            shardCount: 1);
        var asyncSession = new RadarProcessingAsyncRebalanceSession(asyncRebalance);
        var otherAsyncSession = new RadarProcessingAsyncRebalanceSession(otherAsyncRebalance);

        try
        {
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedRebalanceSession(
                    syncRebalance,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncSession));
            Assert.Throws<ArgumentNullException>(() =>
                new RadarProcessingQueuedRebalanceSession(
                    asyncRebalance,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncRebalanceSession: null));
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedRebalanceSession(
                    asyncRebalance,
                    new RadarProcessingOwnedBatchQueue(),
                    otherAsyncSession));
        }
        finally
        {
            await asyncSession.DisposeAsync();
            await otherAsyncSession.DisposeAsync();
        }
    }

}
