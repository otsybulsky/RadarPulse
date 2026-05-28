using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingHandlerDeltaPerformanceGateTests
{
    [Fact]
    public async Task HandlerHeavyOrderedDeltaMergeGateMatchesSequentialFallbackAndCapturesEvidence()
    {
        const int sourceCount = 8;
        const int batchCount = 24;
        const int eventsPerBatch = 64;
        const int payloadBytesPerEvent = 4;

        var universe = CreateUniverse(sourceCount);
        var batches = Enumerable.Range(0, batchCount)
            .Select(index => CreateBatch(
                universe.Version,
                sourceCount,
                eventsPerBatch,
                payloadBytesPerEvent,
                messageTimestampBase: 10_000L * index))
            .ToArray();
        var mergeCore = CreateCore(universe, new HandlerHeavySummaryHandler());
        var sequentialCore = CreateCore(universe, new HandlerHeavySummaryHandler());
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 8, recentDetailCapacity: 32));

        var merge = await MeasureAsync(
            runner,
            universe,
            mergeCore,
            batches,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 4),
            options);
        var sequential = await MeasureAsync(
            runner,
            universe,
            sequentialCore,
            batches,
            RadarProcessingOrderedConcurrencyOptions.Sequential,
            options);

        Assert.True(merge.Runtime.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.False(sequential.Runtime.Plan.AllowsOrderedConcurrentHandlerDeltaMerge);
        Assert.True(merge.Runtime.OverlapResult.IsCompleted);
        Assert.True(sequential.Runtime.OverlapResult.IsCompleted);
        Assert.Equal(
            sequentialCore.CreateSourceSnapshots(),
            mergeCore.CreateSourceSnapshots());
        Assert.Equal(
            sequentialCore.CreateSourceHandlerSnapshots().SelectMany(static snapshot => snapshot.Values),
            mergeCore.CreateSourceHandlerSnapshots().SelectMany(static snapshot => snapshot.Values));
        Assert.Equal(sequentialCore.CreateMetrics(), mergeCore.CreateMetrics());
        Assert.True(merge.Elapsed > TimeSpan.Zero);
        Assert.True(sequential.Elapsed > TimeSpan.Zero);
        Assert.True(merge.AllocatedBytes >= 0);
        Assert.True(sequential.AllocatedBytes >= 0);
        Assert.True(merge.Run.Diagnostics.UsesOrderedHandlerDeltaMerge);
        Assert.True(merge.Run.Diagnostics.IsReady);
        Assert.Equal(0, merge.Run.Diagnostics.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, merge.Run.Diagnostics.CurrentCombinedRetainedPayloadBytes);
        Assert.All(merge.Run.Batches, static batch => Assert.True(batch.IsSuccessful));
    }
}
