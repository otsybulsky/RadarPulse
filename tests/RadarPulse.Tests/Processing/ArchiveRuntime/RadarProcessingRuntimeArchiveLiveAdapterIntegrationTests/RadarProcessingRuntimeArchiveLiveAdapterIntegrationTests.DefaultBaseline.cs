using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests
{
    [Fact]
    public async Task LiveAdapterShapeCompletesThroughDefaultBaseline()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 1, 2, 3], messageTimestampBase: 100),
                CreateEightBitBatch(universe.Version, [4, 5, 6, 7], messageTimestampBase: 200),
                CreateEightBitBatch(universe.Version, [0, 2, 4, 6], messageTimestampBase: 300)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunRebalanceAsync(adapter.PublishTo, session);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CanceledBatchCount);
        Assert.Equal(0, result.QueueTelemetry.SkippedAfterFaultCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.All(result.Consumer.SessionResult.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.True(processing.RebalanceResult?.Validation.IsValid);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);

            var workerCounters = processing.RebalanceResult?.WorkerTelemetry?.Counters;
            Assert.NotNull(workerCounters);
            Assert.Equal(0, workerCounters.FailedBatchCount);
            Assert.Equal(0, workerCounters.CanceledBatchCount);
            Assert.Equal(0, workerCounters.TimedOutBatchCount);
            Assert.Equal(0, workerCounters.RejectedDispatchCount);
            Assert.Equal(0, workerCounters.FailedWorkItemCount);
            Assert.Equal(0, workerCounters.CanceledWorkItemCount);
        });

        var lastProcessing = result.Consumer.SessionResult.ProcessingResults[^1];
        Assert.Equal(3, lastProcessing.ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            lastProcessing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            lastProcessing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
    }

    [Fact]
    public async Task LiveAdapterShapeCompletesThroughOrderedConcurrentProcessingDefaultBaseline()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 1, 2, 3], messageTimestampBase: 100),
                CreateEightBitBatch(universe.Version, [4, 5, 6, 7], messageTimestampBase: 200),
                CreateEightBitBatch(universe.Version, [0, 2, 4, 6], messageTimestampBase: 300)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunProcessingAsync(adapter.PublishTo, core);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static processing => processing.Sequence.Value)
            .ToArray());
        Assert.All(result.Consumer.SessionResult.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.ProcessingResult?.WorkerTelemetry);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
                processing.ProcessingResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
                processing.ProcessingResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.ProcessingResult?.WorkerTelemetry?.Counters.RejectedDispatchCount);
        });
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
    }

    [Fact]
    public async Task LiveAdapterShapeCompletesThroughOrderedConcurrentRebalanceDefaultBaseline()
    {
        var universe = CreateUniverse(sourceCount: 8);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 8,
            shardCount: 4);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1], messageTimestampBase: 100),
                CreateEightBitBatch(universe.Version, [2, 3, 4, 5], messageTimestampBase: 200),
                CreateEmptyBatch(universe.Version)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunOrderedRebalanceAsync(adapter.PublishTo, session);

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static processing => processing.Sequence.Value)
            .ToArray());
        Assert.All(result.Consumer.SessionResult.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.True(processing.RebalanceResult?.Validation.IsValid);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
                processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(
                RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
                processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.RebalanceResult?.WorkerTelemetry?.Counters.RejectedDispatchCount);
        });
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.True(result.Consumer.SessionResult.FinalTopologyVersion?.Value >= RadarProcessingTopologyVersion.Initial.Value);
    }
}
