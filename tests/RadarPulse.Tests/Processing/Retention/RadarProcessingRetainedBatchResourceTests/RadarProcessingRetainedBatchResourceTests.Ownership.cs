using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedBatchResourceTests
{
    [Fact]
    public void RetainedBatchResourceStateValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingRetainedBatchResourceState.ProviderOwned);
        Assert.Equal(2, (int)RadarProcessingRetainedBatchResourceState.QueueOwned);
        Assert.Equal(3, (int)RadarProcessingRetainedBatchResourceState.ConsumerOwned);
        Assert.Equal(4, (int)RadarProcessingRetainedBatchResourceState.Released);
        Assert.Equal(5, (int)RadarProcessingRetainedBatchResourceState.ReleaseFailed);
    }

    [Fact]
    public void RetainedQueuedBatchTransfersProviderResourceToQueueOwnership()
    {
        var queued = CreateQueuedBatch();
        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: queued.PayloadBytes);

        var retained = new RadarProcessingRetainedQueuedBatch(queued, resource);

        Assert.Same(queued, retained.QueuedBatch);
        Assert.Same(resource, retained.Resource);
        Assert.Equal(queued.Sequence, retained.Sequence);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.QueueOwned, resource.State);
        Assert.False(retained.HasTerminalResource);

        Assert.Throws<InvalidOperationException>(resource.TransferToQueue);
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingRetainedQueuedBatch(null!));
    }
}
