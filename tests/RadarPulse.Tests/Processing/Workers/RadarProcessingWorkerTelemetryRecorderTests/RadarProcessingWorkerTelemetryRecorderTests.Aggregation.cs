using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerTelemetryRecorderTests
{
    [Fact]
    public async Task RecorderAggregatesSuccessfulDispatchCountersAndTiming()
    {
        await using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);
        var dispatch = await dispatcher.DispatchAsync(
            batchSequence: 7,
            batch,
            (borrowedBatch, route, workItem, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shard = route.GetShard(workItem.ShardId);
                return ValueTask.FromResult(
                    RadarProcessingAsyncWorkCompletion.Succeeded(
                        workItem,
                        processedStreamEventCount: shard.EventIndexes.Length,
                        processedPayloadValueCount: shard.Metrics.PayloadValueCount));
            });
        var recorder = new RadarProcessingWorkerTelemetryRecorder();

        recorder.RecordDispatch(
            dispatch,
            dispatchTime: TimeSpan.FromMilliseconds(9),
            aggregationTime: TimeSpan.FromMilliseconds(3));
        var summary = recorder.CreateSummary();

        Assert.Equal(2, summary.WorkerCount);
        Assert.Equal(1, summary.QueueCapacity);
        Assert.Equal(1, summary.Counters.DispatchedBatchCount);
        Assert.Equal(1, summary.Counters.CompletedBatchCount);
        Assert.Equal(0, summary.Counters.FailedBatchCount);
        Assert.Equal(0, summary.Counters.CanceledBatchCount);
        Assert.Equal(2, summary.Counters.SubmittedWorkItemCount);
        Assert.Equal(2, summary.Counters.AcceptedWorkItemCount);
        Assert.Equal(2, summary.Counters.CompletedWorkItemCount);
        Assert.Equal(2, summary.Counters.SucceededWorkItemCount);
        Assert.Equal(TimeSpan.FromMilliseconds(9), summary.Counters.TotalDispatchTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3), summary.Counters.TotalAggregationTime);
        Assert.Equal(dispatch.BatchResult!.Completion.TotalQueueWaitTime, summary.Counters.TotalQueueWaitTime);
        Assert.Equal(dispatch.BatchResult.Completion.TotalExecutionTime, summary.Counters.TotalExecutionTime);
        Assert.Equal(dispatch.DrainResult.BarrierWaitTime, summary.Counters.TotalBarrierWaitTime);
        Assert.Single(summary.RecentBatches);
        Assert.Empty(summary.RecentFailures);
        Assert.Equal(7, summary.RecentBatches[0].BatchSequence);
        Assert.True(summary.RecentBatches[0].IsSuccessful);
    }
}
