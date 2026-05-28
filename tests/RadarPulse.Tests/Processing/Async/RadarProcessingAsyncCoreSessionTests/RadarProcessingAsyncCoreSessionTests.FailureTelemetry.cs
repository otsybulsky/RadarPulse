using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCoreSessionTests
{
    [Fact]
    public async Task AsyncCoreSessionRejectsCapacityTooSmallWithoutStateMutationAndReportsWorkerTelemetry()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 1, queueCapacity: 1));
        var batch = CreateMixedBatch(universe.Version);

        await using var session = new RadarProcessingAsyncCoreSession(core);
        var result = await session.ProcessAsync(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.MetricsMismatch, result.Validation.Error);
        Assert.Equal(RadarProcessingMetrics.Empty, result.Metrics);
        Assert.Equal(RadarProcessingMetrics.Empty, core.CreateMetrics());
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(1, result.WorkerTelemetry.Counters.DispatchedBatchCount);
        Assert.Equal(1, result.WorkerTelemetry.Counters.FailedBatchCount);
        Assert.Equal(1, result.WorkerTelemetry.Counters.RejectedDispatchCount);
        Assert.Equal(0, result.WorkerTelemetry.Counters.AcceptedWorkItemCount);
        Assert.Single(result.WorkerTelemetry.RecentFailures);
        Assert.Equal(RadarProcessingAsyncFailureKind.EnqueueRejected, result.WorkerTelemetry.RecentFailures[0].FailureKind);
    }

    [Fact]
    public async Task AsyncCoreSessionReportsSourceOrderViolationWithoutCountingBatchComplete()
    {
        var universe = CreateUniverse(sourceCount: 2);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 2,
            shardCount: 1);
        var batch = CreateBatch(
            universe.Version,
            new[]
            {
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 100, payloadOffset: 0),
                CreateEvent(sourceId: 1, messageTimestampUtcTicks: 99, payloadOffset: 4)
            },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        await using var session = new RadarProcessingAsyncCoreSession(core);
        var result = await session.ProcessAsync(batch);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingValidationError.SourceOrderViolation, result.Validation.Error);
        Assert.Equal(1, result.Validation.SourceId);
        Assert.Equal(1, result.Validation.EventIndex);
        Assert.Equal(0, result.Metrics.ProcessedBatchCount);
        Assert.Equal(1, result.Metrics.ProcessedStreamEventCount);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(1, result.WorkerTelemetry.Counters.FailedWorkItemCount);
    }
}
