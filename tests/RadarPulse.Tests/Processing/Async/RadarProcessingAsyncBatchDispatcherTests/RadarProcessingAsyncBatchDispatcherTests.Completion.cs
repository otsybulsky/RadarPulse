using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncBatchDispatcherTests
{
    [Fact]
    public async Task DispatchCompletesOnlyAfterWorkerGroupCompletionBarrier()
    {
        await using var group = CreateStartedGroup(workerCount: 1, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 1, partitionCount: 1, shardCount: 1);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0]);
        var startedExecution = CreateSignal();
        var releaseExecution = CreateSignal();

        var dispatch = dispatcher.DispatchAsync(
            batchSequence: 3,
            batch,
            async (borrowedBatch, route, workItem, _) =>
            {
                Assert.Same(batch, borrowedBatch);
                Assert.Equal(topology.Version, route.TopologyVersion);
                startedExecution.SetResult();
                await releaseExecution.Task.ConfigureAwait(false);
                return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
            }).AsTask();

        await startedExecution.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(dispatch.IsCompleted);

        releaseExecution.SetResult();
        var result = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.True(result.DrainResult.IsDrained);
        Assert.Equal(1, result.BatchResult?.Completion.SucceededWorkItemCount);
    }

    [Fact]
    public async Task DispatchReportsWorkerTimingAndCompletionStatus()
    {
        await using var group = CreateStartedGroup(workerCount: 2, queueCapacity: 1);
        var topology = CreateTopology(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var dispatcher = new RadarProcessingAsyncBatchDispatcher(group, () => topology);
        var batch = CreateEightBitBatch(topology.SourceUniverseVersion, sourceIds: [0, 1, 2, 3]);

        var result = await dispatcher.DispatchAsync(
            batchSequence: 4,
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

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.BatchResult);
        Assert.Equal(topology.ShardCount, result.BatchResult.Completion.SucceededWorkItemCount);
        Assert.Equal(batch.EventCount, result.BatchResult.Completion.ProcessedStreamEventCount);
        Assert.Equal(batch.EventCount, result.BatchResult.Completion.ProcessedPayloadValueCount);
        Assert.True(result.DrainResult.IsDrained);
        Assert.Equal(topology.ShardCount, result.DrainResult.AcceptedWorkItemCount);
        Assert.Equal(topology.ShardCount, result.DrainResult.CompletedWorkItemCount);
        Assert.True(result.DrainResult.BarrierWaitTime >= TimeSpan.Zero);
    }
}
