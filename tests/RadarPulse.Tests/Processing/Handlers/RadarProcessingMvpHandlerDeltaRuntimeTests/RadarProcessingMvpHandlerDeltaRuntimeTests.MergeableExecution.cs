using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    [Fact]
    public async Task MvpProcessingUsesOrderedDeltaMergeForMergeableHandlers()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreateCore(universe, new MergeableCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunMvpProcessingAsync(
            CreateProducer(
                universe,
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateBatch(universe.Version, [0, 1], messageTimestampBase: 200)),
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));

        Assert.False(result.Plan.UsedSequentialFallback);
        Assert.True(result.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.True(result.OverlapResult.IsCompleted);
        Assert.Equal(2, result.OverlapResult.Consumer.SessionResult.ProcessingResults.Count);
        Assert.All(
            result.OverlapResult.Consumer.SessionResult.ProcessingResults,
            processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(2, core.GetSourceHandlerSnapshot(sourceId: 0).Values[0].Int64Value);
        Assert.Equal(2, core.GetSourceHandlerSnapshot(sourceId: 1).Values[0].Int64Value);
    }
}
