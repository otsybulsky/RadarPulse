using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedBatchResourceTests
{
    [Fact]
    public void ConsumerLeaseTransfersResourceAndReleasesExactlyOnce()
    {
        var releaseCount = 0;
        var queued = CreateQueuedBatch();
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: queued.PayloadBytes,
            release: () =>
            {
                releaseCount++;
                return RadarProcessingRetainedPayloadReleaseResult.Released(
                    RadarProcessingRetainedPayloadStrategy.PooledCopy,
                    TimeSpan.FromMilliseconds(1),
                    payloadBytes: queued.PayloadBytes);
            });
        var retained = new RadarProcessingRetainedQueuedBatch(queued, resource);

        using var lease = retained.AcquireForConsumer();

        Assert.Same(retained, lease.RetainedBatch);
        Assert.Same(queued, lease.QueuedBatch);
        Assert.Same(queued.Batch, lease.Batch);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.ConsumerOwned, resource.State);

        var released = lease.Release();
        var second = resource.Release();

        Assert.True(lease.IsDisposed);
        Assert.True(released.IsReleased);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, released.Status);
        Assert.Equal(queued.PayloadBytes, released.PayloadBytes);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased, second.Status);
        Assert.Equal(1, releaseCount);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, resource.State);
        Assert.True(resource.IsTerminal);
        Assert.Same(released, resource.LastReleaseResult);
    }
}
