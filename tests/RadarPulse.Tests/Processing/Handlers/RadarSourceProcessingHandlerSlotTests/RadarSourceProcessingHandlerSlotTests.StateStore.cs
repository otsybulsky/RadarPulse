using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarSourceProcessingHandlerSlotTests
{
    [Fact]
    public void StateStoreKeepsHandlerStateIsolatedBySource()
    {
        var handler = new CountingHandler();
        var store = new RadarSourceProcessingStateStore(
            CreateUniverse(sourceCount: 2),
            new IRadarSourceProcessingHandler[] { handler });
        var firstBatch = CreateBatch(new[] { 0, 1, 0 });

        ApplyEvent(store, firstBatch, eventIndex: 0);
        ApplyEvent(store, firstBatch, eventIndex: 1);
        ApplyEvent(store, firstBatch, eventIndex: 2);

        var sourceZero = store.GetHandlerSnapshot(sourceId: 0);
        var sourceOne = store.GetHandlerSnapshot(sourceId: 1);

        Assert.Equal(2, GetInt64(sourceZero, "events"));
        Assert.Equal(2, GetInt64(sourceZero, "payload.values"));
        Assert.Equal(4, GetInt64(sourceZero, "raw.checksum"));
        Assert.Equal(1, GetInt64(sourceOne, "events"));
        Assert.Equal(1, GetInt64(sourceOne, "payload.values"));
        Assert.Equal(2, GetInt64(sourceOne, "raw.checksum"));
        Assert.Equal(3, handler.InvocationCount);
    }

    [Fact]
    public void StateStoreRequiresPayloadAwareApplyWhenHandlersAreConfigured()
    {
        var store = new RadarSourceProcessingStateStore(
            CreateUniverse(sourceCount: 1),
            new IRadarSourceProcessingHandler[] { new PayloadCapturingHandler() });
        var batch = CreateBatch(new[] { 0 });

        Assert.Throws<InvalidOperationException>(() =>
            store.ApplyProcessedEvent(
                batch.Events.Span[0],
                processedPayloadValueCount: 1,
                rawValueChecksum: 1));
    }
}
