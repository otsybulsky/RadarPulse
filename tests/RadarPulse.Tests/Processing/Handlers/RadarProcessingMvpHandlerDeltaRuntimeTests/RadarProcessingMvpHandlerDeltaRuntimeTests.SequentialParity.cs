using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    [Fact]
    public async Task OrderedDeltaMergeOutputMatchesSequentialFallbackOutput()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var mergeCore = CreateCore(universe, new MergeableCountingHandler());
        var sequentialCore = CreateCore(universe, new MergeableCountingHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var batches = new[]
        {
            CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
            CreateBatch(universe.Version, [1], messageTimestampBase: 200),
            CreateBatch(universe.Version, [0], messageTimestampBase: 300)
        };

        await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            mergeCore,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));
        await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            sequentialCore,
            RadarProcessingOrderedConcurrencyOptions.Sequential);

        Assert.Equal(
            sequentialCore.CreateSourceSnapshots(),
            mergeCore.CreateSourceSnapshots());
        Assert.Equal(
            sequentialCore.CreateSourceHandlerSnapshots().Select(static snapshot => snapshot.Values[0].Int64Value),
            mergeCore.CreateSourceHandlerSnapshots().Select(static snapshot => snapshot.Values[0].Int64Value));
        Assert.Equal(sequentialCore.CreateMetrics(), mergeCore.CreateMetrics());
    }

    [Fact]
    public async Task OrderedDeltaMergeWithBenchmarkAccumulatorHandlersMatchesSequentialFallbackOutput()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var mergeCore = CreateCore(
            universe,
            RadarProcessingBenchmarkHandlers.Create(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy));
        var sequentialCore = CreateCore(
            universe,
            RadarProcessingBenchmarkHandlers.Create(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy));
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var batches = new[]
        {
            CreateBatch(universe.Version, [0, 1], messageTimestampBase: 100),
            CreateBatch(universe.Version, [1], messageTimestampBase: 200),
            CreateBatch(universe.Version, [0], messageTimestampBase: 300)
        };

        var merge = await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            mergeCore,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4));
        var sequential = await runner.RunMvpProcessingAsync(
            CreateProducer(universe, batches),
            sequentialCore,
            RadarProcessingOrderedConcurrencyOptions.Sequential);

        Assert.True(merge.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.True(sequential.Plan.HandlerOutputContract.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.Equal(
            sequentialCore.CreateSourceSnapshots(),
            mergeCore.CreateSourceSnapshots());
        Assert.Equal(
            sequentialCore.CreateSourceHandlerSnapshots().SelectMany(static snapshot => snapshot.Values),
            mergeCore.CreateSourceHandlerSnapshots().SelectMany(static snapshot => snapshot.Values));
        Assert.Equal(sequentialCore.CreateMetrics(), mergeCore.CreateMetrics());
    }
}
