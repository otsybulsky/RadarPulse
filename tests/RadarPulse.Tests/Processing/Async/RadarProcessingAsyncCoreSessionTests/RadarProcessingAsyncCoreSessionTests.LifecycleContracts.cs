using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCoreSessionTests
{
    [Fact]
    public async Task AsyncCoreSessionOwnsAndDisposesDefaultWorkerGroup()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 1,
            shardCount: 1);
        var session = new RadarProcessingAsyncCoreSession(core);

        await session.ProcessAsync(CreateEmptyBatch(universe.Version));
        await session.DisposeAsync();

        Assert.Equal(RadarProcessingWorkerGroupState.Disposed, session.WorkerGroup.Status.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await session.ProcessAsync(CreateEmptyBatch(universe.Version)));
    }

    [Fact]
    public void AsyncCoreSessionRejectsNonAsyncCore()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var partitioned = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 1,
            shardCount: 1);

        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncCoreSession(null!));
        Assert.Throws<ArgumentException>(() => new RadarProcessingAsyncCoreSession(partitioned));
    }
}
