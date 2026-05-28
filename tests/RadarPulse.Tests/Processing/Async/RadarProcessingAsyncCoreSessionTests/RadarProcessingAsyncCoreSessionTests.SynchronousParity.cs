using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCoreSessionTests
{
    [Fact]
    public async Task AsyncCoreSessionMatchesSynchronousPartitionedMetricsAndSnapshots()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var partitioned = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 6,
            shardCount: 3);
        var asyncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var batch = CreateMixedBatch(universe.Version);
        var asyncBefore = asyncCore.CreateSourceSnapshots();
        var asyncPreviousMetrics = asyncCore.CreateMetrics();

        var partitionedResult = partitioned.Process(batch);
        await using var session = new RadarProcessingAsyncCoreSession(asyncCore);
        var asyncResult = await session.ProcessAsync(batch);
        var asyncValidation = RadarProcessingOutputValidator.Validate(
            batch,
            asyncResult,
            asyncBefore,
            asyncCore.CreateSourceSnapshots(),
            asyncPreviousMetrics);

        Assert.True(partitionedResult.IsValid);
        Assert.True(asyncResult.IsValid);
        Assert.True(asyncValidation.IsValid);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.ExecutionMode);
        Assert.Equal(partitionedResult.Metrics, asyncResult.Metrics);
        Assert.Equal(partitioned.CreateSourceSnapshots(), asyncCore.CreateSourceSnapshots());
        Assert.NotNull(asyncResult.Telemetry);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asyncResult.Telemetry.ExecutionMode);
        Assert.Equal(partitionedResult.Telemetry?.BatchMetrics, asyncResult.Telemetry.BatchMetrics);
        Assert.NotNull(asyncResult.WorkerTelemetry);
        Assert.Equal(3, asyncResult.WorkerTelemetry.WorkerCount);
        Assert.Equal(1, asyncResult.WorkerTelemetry.QueueCapacity);
        Assert.Equal(1, asyncResult.WorkerTelemetry.Counters.DispatchedBatchCount);
        Assert.Equal(1, asyncResult.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(3, asyncResult.WorkerTelemetry.Counters.SubmittedWorkItemCount);
        Assert.Equal(3, asyncResult.WorkerTelemetry.Counters.SucceededWorkItemCount);
        Assert.Single(asyncResult.WorkerTelemetry.RecentBatches);
        Assert.Empty(asyncResult.WorkerTelemetry.RecentFailures);
    }
}
