using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticBenchmarkTests
{
    [Fact]
    public void AsyncSyntheticBenchmarkReportsWorkerTelemetryAndValidation()
    {
        var workload = RadarProcessingSyntheticWorkload.Create(
            new RadarProcessingSyntheticWorkloadOptions(
                SourceCount: 6,
                BatchCount: 2,
                EventsPerBatch: 18,
                PayloadValuesPerEvent: 3));

        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workload,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            RadarProcessingBenchmarkHandlerSet.None,
            iterations: 2,
            warmupIterations: 1,
            CancellationToken.None,
            new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));

        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ExecutionMode);
        Assert.Equal(RadarProcessingValidationProfile.Benchmark, result.ValidationProfile);
        Assert.Equal(2, result.BatchesPerIteration);
        Assert.Equal(4, result.TotalBatches);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(3, result.WorkerTelemetry.WorkerCount);
        Assert.Equal(1, result.WorkerTelemetry.QueueCapacity);
        Assert.Equal(4, result.WorkerTelemetry.Counters.DispatchedBatchCount);
        Assert.Equal(4, result.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(12, result.WorkerTelemetry.Counters.SubmittedWorkItemCount);
        Assert.Equal(12, result.WorkerTelemetry.Counters.SucceededWorkItemCount);
        Assert.NotNull(result.AsyncValidation);
        Assert.True(result.AsyncValidation.IsValid);
        Assert.True(result.AsyncValidation.HasComparisonChecksums);
        Assert.Equal(result.AsyncValidation.SynchronousChecksum, result.AsyncValidation.AsyncChecksum);
    }

    [Fact]
    public void AsyncSyntheticBenchmarkMatchesPartitionedComparableFields()
    {
        var workload = RadarProcessingSyntheticWorkload.Create(
            new RadarProcessingSyntheticWorkloadOptions(
                SourceCount: 6,
                BatchCount: 2,
                EventsPerBatch: 18,
                PayloadValuesPerEvent: 3));
        var benchmark = new RadarProcessingSyntheticBenchmark();

        var partitioned = benchmark.Measure(
            workload,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 6,
            shardCount: 3,
            RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            iterations: 1,
            warmupIterations: 0,
            CancellationToken.None);
        var asyncResult = benchmark.Measure(
            workload,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            iterations: 1,
            warmupIterations: 0,
            CancellationToken.None,
            new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));

        Assert.Equal(partitioned.BatchesPerIteration, asyncResult.BatchesPerIteration);
        Assert.Equal(partitioned.EventsPerIteration, asyncResult.EventsPerIteration);
        Assert.Equal(partitioned.PayloadValuesPerIteration, asyncResult.PayloadValuesPerIteration);
        Assert.Equal(partitioned.RawValueChecksumPerIteration, asyncResult.RawValueChecksumPerIteration);
        Assert.Equal(partitioned.ActiveSourceCount, asyncResult.ActiveSourceCount);
        Assert.Equal(partitioned.ValidationChecksum, asyncResult.ValidationChecksum);
        Assert.Equal(partitioned.ShardDistributions, asyncResult.ShardDistributions);
        Assert.True(asyncResult.PayloadValuesPerSecond > 0);
    }
}
