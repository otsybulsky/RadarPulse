using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSessionTests
{
    [Fact]
    public async Task OrderedConcurrentAsyncRebalancePreservesWorkerTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        await using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(
                universe,
                RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)),
            new RadarProcessingProviderQueueOptions(capacity: 2));

        await session.EnqueueAsync(CreateEmptyBatch(universe.Version));
        await session.EnqueueAsync(CreateEmptyBatch(universe.Version));
        session.CompleteAdding();
        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.All(result.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
            Assert.Equal(2, processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(4, processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.RebalanceResult?.WorkerTelemetry?.Counters.RejectedDispatchCount);
            Assert.Equal(0, processing.RebalanceResult?.WorkerTelemetry?.Counters.FailedBatchCount);
        });
    }

    [Fact]
    public async Task OrderedConcurrentRebalanceFailsClosedAndSkipsLaterActiveSuccess()
    {
        var universe = CreateUniverse(sourceCount: 1);
        await using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1),
            new RadarProcessingProviderQueueOptions(capacity: 2));

        await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        await session.EnqueueAsync(CreateEightBitBatch(universe.Version, [0]));
        session.CompleteAdding();

        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsFaulted);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.ProcessingResults.Select(static processing => processing.Status).ToArray());
        Assert.Equal(1, result.Telemetry.FailedBatchCount);
        Assert.Equal(1, result.Telemetry.SkippedAfterFaultCount);
    }

    [Fact]
    public async Task OrderedConcurrentRebalanceCancellationBeforeDequeueReturnsCanceled()
    {
        var universe = CreateUniverse(sourceCount: 1);
        await using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2),
            cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
        Assert.Empty(result.ProcessingResults);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentCombinedRetainedPayloadBytes);
    }

}
