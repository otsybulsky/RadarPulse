using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCompletionAggregatorTests
{
    [Fact]
    public async Task AsyncAggregationCanProduceResultComparableWithSynchronousPartitionedOutput()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var syncCore = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2));
        var asyncStore = new RadarSourceProcessingStateStore(universe);
        var beforeSnapshots = asyncStore.CreateSnapshots();
        var previousMetrics = asyncStore.CreateMetrics();
        await using var group = CreateStartedGroup(workerCount: 1, queueCapacity: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => syncCore.Topology);
        var batch = CreateEightBitBatch(universe.Version, sourceIds: [0, 1, 2, 3]);

        var syncResult = syncCore.Process(batch);
        var dispatchResult = await dispatcher.DispatchAsync(
            batchSequence: 1,
            batch,
            (borrowedBatch, route, workItem, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shard = route.GetShard(workItem.ShardId);
                foreach (var eventIndex in shard.EventIndexes.Span)
                {
                    var streamEvent = borrowedBatch.Events.Span[eventIndex];
                    var payloadMetrics = route.GetRoutedEvent(eventIndex).PayloadMetrics;
                    asyncStore.ApplyProcessedEvent(
                        streamEvent,
                        payloadMetrics.PayloadValueCount,
                        payloadMetrics.RawValueChecksum);
                }

                return ValueTask.FromResult(
                    RadarProcessingAsyncWorkCompletion.Succeeded(
                        workItem,
                        processedStreamEventCount: shard.EventIndexes.Length,
                        processedPayloadValueCount: shard.Metrics.PayloadValueCount));
            });

        var aggregation = new RadarProcessingAsyncCompletionAggregator().Aggregate(dispatchResult);
        var asyncResult = aggregation.CreateProcessingResult(asyncStore.CreateMetrics(processedBatchCount: 1));
        var validation = RadarProcessingOutputValidator.Validate(
            batch,
            asyncResult,
            beforeSnapshots,
            asyncStore.CreateSnapshots(),
            previousMetrics);

        Assert.True(aggregation.IsSuccess);
        Assert.True(asyncResult.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.ExecutionMode);
        Assert.Equal(syncResult.Metrics, asyncResult.Metrics);
        Assert.NotNull(asyncResult.Telemetry);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.Telemetry.ExecutionMode);
        Assert.Equal(syncResult.Telemetry?.BatchMetrics, asyncResult.Telemetry.BatchMetrics);
        Assert.True(validation.IsValid);
    }
}
