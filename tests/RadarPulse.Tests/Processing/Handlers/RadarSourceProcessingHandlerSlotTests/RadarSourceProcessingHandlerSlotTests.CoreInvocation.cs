using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarSourceProcessingHandlerSlotTests
{
    [Fact]
    public void CoreInvokesConfiguredHandlerAndProjectsSnapshots()
    {
        var handler = new CountingHandler();
        var universe = CreateUniverse(sourceCount: 2);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(handlers: new IRadarSourceProcessingHandler[] { handler }));
        var batch = CreateBatch(new[] { 0, 1 });

        var result = core.Process(batch);
        var sourceZero = core.GetSourceHandlerSnapshot(sourceId: 0);
        var sourceOne = core.GetSourceHandlerSnapshot(sourceId: 1);

        Assert.True(result.IsValid);
        Assert.Equal(2, handler.InvocationCount);
        Assert.Equal(1, GetInt64(sourceZero, "events"));
        Assert.Equal(1, GetInt64(sourceZero, "payload.values"));
        Assert.Equal(1, GetInt64(sourceZero, "raw.checksum"));
        Assert.Equal(1, GetInt64(sourceOne, "events"));
        Assert.Equal(1, GetInt64(sourceOne, "payload.values"));
        Assert.Equal(2, GetInt64(sourceOne, "raw.checksum"));
        Assert.Equal(1.0, GetDouble(sourceOne, "last.scale"));
    }

    [Fact]
    public void HandlerReceivesEventPayloadSpan()
    {
        var handler = new PayloadCapturingHandler();
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(handlers: new IRadarSourceProcessingHandler[] { handler }));
        var batch = CreateBatch(new[] { 0 });

        var result = core.Process(batch);

        Assert.True(result.IsValid);
        Assert.Equal(1, handler.LastPayloadLength);
        Assert.Equal(1, handler.LastFirstPayloadByte);
        Assert.Equal(1, handler.LastPayloadValueCount);
        Assert.Equal(1, handler.InvocationCount);
    }

    [Fact]
    public void NoConfiguredHandlersKeepsBasePathValid()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = new RadarProcessingCore(universe);
        var batch = CreateBatch(new[] { 0 });

        var result = core.Process(batch);
        var handlerSnapshot = core.GetSourceHandlerSnapshot(sourceId: 0);

        Assert.True(result.IsValid);
        Assert.Empty(core.Options.Handlers);
        Assert.Empty(handlerSnapshot.Values);
    }
}
