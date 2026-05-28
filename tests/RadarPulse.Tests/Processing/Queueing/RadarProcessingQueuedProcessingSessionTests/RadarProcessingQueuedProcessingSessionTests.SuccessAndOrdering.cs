using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProcessingSessionTests
{
    [Fact]
    public async Task QueuedSyncProcessingMatchesDirectCoreResult()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var directCore = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        var queuedCore = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        var batch = CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1);

        var directResult = directCore.Process(batch);
        using var session = new RadarProcessingQueuedProcessingSession(queuedCore);
        var enqueue = await session.EnqueueAsync(batch);
        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Completed, result.Status);
        Assert.Single(result.EnqueueResults);
        Assert.Single(result.ProcessingResults);
        Assert.True(result.ProcessingResults[0].IsSuccessful);
        Assert.Same(enqueue.Batch, result.EnqueueResults[0].Batch);
        Assert.Equal(directResult.Metrics, result.ProcessingResults[0].ProcessingResult?.Metrics);
        Assert.Equal(directCore.CreateSourceSnapshots(), queuedCore.CreateSourceSnapshots());
        Assert.Equal(1, result.Telemetry.EnqueueAttemptCount);
        Assert.Equal(1, result.Telemetry.EnqueuedBatchCount);
        Assert.Equal(1, result.Telemetry.DequeuedBatchCount);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
        Assert.Equal(0, result.Telemetry.CanceledBatchCount);
    }

    [Fact]
    public async Task QueuedAsyncProcessingMatchesDirectAsyncResult()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var asyncOptions = new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1);
        var directCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 4,
            shardCount: 2,
            asyncOptions);
        var queuedCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 4,
            shardCount: 2,
            asyncOptions);
        var batch = CreateMixedBatch(universe.Version);

        await using var directSession = new RadarProcessingAsyncCoreSession(directCore);
        var directResult = await directSession.ProcessAsync(batch);
        await using var queuedSession = new RadarProcessingQueuedProcessingSession(
            queuedCore,
            new RadarProcessingProviderQueueOptions(capacity: 1));
        var enqueue = await queuedSession.EnqueueAsync(batch);
        queuedSession.CompleteAdding();
        var result = await queuedSession.DrainAsync();

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
        Assert.Equal(directResult.Metrics, processing.ProcessingResult?.Metrics);
        Assert.Equal(directCore.CreateSourceSnapshots(), queuedCore.CreateSourceSnapshots());
        Assert.NotNull(processing.ProcessingResult?.WorkerTelemetry);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
    }

    [Fact]
    public async Task QueuedProcessingPreservesProviderSequenceOrder()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var first = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));
        var second = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 101, firstPayloadValue: 3));
        session.CompleteAdding();

        var result = await session.DrainAsync();

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static processing => processing.Sequence.Value));
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(2, result.ProcessingResults[^1].ProcessingResult?.Metrics.ProcessedBatchCount);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.Equal(2, result.Telemetry.DequeuedBatchCount);
    }
}
