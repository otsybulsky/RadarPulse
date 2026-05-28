using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDurableRebalanceSessionTests
{
    [Fact]
    public async Task DurableAsyncRebalancePreservesWorkerTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var queue = new RadarProcessingDurableEnvelopeQueue();
        await using var session = new RadarProcessingDurableRebalanceSession(
            CreateSession(
                universe,
                RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4)),
            queue);

        queue.Accept(BatchId("batch-0"), CreateEmptyBatch(universe.Version));
        queue.Accept(BatchId("batch-1"), CreateEmptyBatch(universe.Version));

        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.QueueSummary.ReleasedEnvelopeCount);
        Assert.All(result.ProcessingResults, static processing =>
        {
            Assert.True(processing.IsSuccessful);
            Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
            Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
            Assert.Equal(2, processing.RebalanceResult?.WorkerTelemetry?.WorkerCount);
            Assert.Equal(4, processing.RebalanceResult?.WorkerTelemetry?.QueueCapacity);
            Assert.Equal(0, processing.RebalanceResult?.WorkerTelemetry?.Counters.FailedBatchCount);
        });
    }
}
