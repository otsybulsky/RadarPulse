using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    [Fact]
    public async Task UnsupportedHandlerSetFailsClosedBeforeMvpProcessingStarts()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, new UnsupportedCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            runner.RunMvpProcessingAsync(
                    CreateProducer(
                        universe,
                        CreateBatch(universe.Version, [0], messageTimestampBase: 100)),
                    core,
                    new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4))
                .AsTask());

        Assert.Contains("Unsupported", exception.Message, StringComparison.Ordinal);
    }
}
