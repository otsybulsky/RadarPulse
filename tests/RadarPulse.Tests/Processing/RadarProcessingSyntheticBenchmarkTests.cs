using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingSyntheticBenchmarkTests
{
    [Fact]
    public void SyntheticBenchmarkMeasuresStableSequentialIterations()
    {
        var workloadOptions = new RadarProcessingSyntheticWorkloadOptions(
            SourceCount: 4,
            BatchCount: 2,
            EventsPerBatch: 8,
            PayloadValuesPerEvent: 2);

        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workloadOptions,
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            RadarProcessingBenchmarkHandlerSet.None,
            iterations: 2,
            warmupIterations: 1,
            CancellationToken.None);

        Assert.Equal(RadarProcessingExecutionMode.Sequential, result.ExecutionMode);
        Assert.Equal(RadarProcessingBenchmarkHandlerSet.None, result.HandlerSet);
        Assert.Equal(2, result.Iterations);
        Assert.Equal(1, result.WarmupIterations);
        Assert.Equal(4, result.SourceCount);
        Assert.Equal(2, result.BatchesPerIteration);
        Assert.Equal(16, result.EventsPerIteration);
        Assert.Equal(32, result.PayloadValuesPerIteration);
        Assert.Equal(4, result.ActiveSourceCount);
        Assert.Equal(32, result.TotalEvents);
        Assert.Equal(64, result.TotalPayloadValues);
        Assert.Single(result.ShardDistributions);
        Assert.Equal(16, result.ShardDistributions[0].EventCount);
        Assert.NotEqual(0UL, result.ValidationChecksum);
        Assert.True(result.Elapsed > TimeSpan.Zero);
        Assert.True(result.EventsPerSecond > 0);
        Assert.True(result.PayloadValuesPerSecond > 0);
        Assert.True(result.AllocatedBytes >= 0);
    }

    [Fact]
    public void SyntheticBenchmarkReportsPartitionedShardDistribution()
    {
        var workload = RadarProcessingSyntheticWorkload.Create(
            new RadarProcessingSyntheticWorkloadOptions(
                SourceCount: 4,
                BatchCount: 1,
                EventsPerBatch: 8,
                PayloadValuesPerEvent: 1));

        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workload,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 4,
            shardCount: 2,
            RadarProcessingBenchmarkHandlerSet.None,
            iterations: 1,
            warmupIterations: 0,
            CancellationToken.None);

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(4, result.PartitionCount);
        Assert.Equal(2, result.ShardCount);
        Assert.Equal(2, result.ShardDistributions.Count);
        Assert.Equal(4, result.ShardDistributions[0].EventCount);
        Assert.Equal(4, result.ShardDistributions[1].EventCount);
    }

    [Fact]
    public void SyntheticBenchmarkExcludesWarmupFromMeasuredTotals()
    {
        var workloadOptions = new RadarProcessingSyntheticWorkloadOptions(
            SourceCount: 2,
            BatchCount: 1,
            EventsPerBatch: 4,
            PayloadValuesPerEvent: 1);

        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workloadOptions,
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            iterations: 3,
            warmupIterations: 2,
            CancellationToken.None);

        Assert.Equal(1, result.BatchesPerIteration);
        Assert.Equal(4, result.EventsPerIteration);
        Assert.Equal(3, result.TotalBatches);
        Assert.Equal(12, result.TotalEvents);
        Assert.Equal(12, result.TotalPayloadValues);
    }

    [Fact]
    public void BenchmarkHandlerFactoryCreatesMergeableCounterChecksumHeavySet()
    {
        var handlers = RadarProcessingBenchmarkHandlers.Create(
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy);

        Assert.Equal(2, handlers.Count);
        Assert.All(
            handlers,
            handler =>
            {
                var metadata = Assert.IsAssignableFrom<IRadarSourceProcessingHandlerExecutionMetadata>(handler);
                Assert.Equal(
                    RadarSourceProcessingHandlerExecutionClassification.Mergeable,
                    metadata.ExecutionClassification);
                var merger = Assert.IsAssignableFrom<IRadarProcessingHandlerDeltaMerger>(handler);
                Assert.Equal(handler.Descriptor.Name, merger.HandlerName);
            });
        var fieldNames = handlers
            .SelectMany(static handler => handler.Descriptor.SnapshotFields)
            .Select(static field => field.Name)
            .ToArray();
        Assert.Equal(fieldNames.Length, fieldNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SyntheticBenchmarkAcceptsCounterChecksumHeavyHandlerSet()
    {
        var workloadOptions = new RadarProcessingSyntheticWorkloadOptions(
            SourceCount: 4,
            BatchCount: 1,
            EventsPerBatch: 8,
            PayloadValuesPerEvent: 4);

        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workloadOptions,
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy,
            iterations: 1,
            warmupIterations: 0,
            CancellationToken.None);

        Assert.Equal(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy, result.HandlerSet);
        Assert.Equal(8, result.EventsPerIteration);
        Assert.Equal(32, result.PayloadValuesPerIteration);
        Assert.NotEqual(0UL, result.ValidationChecksum);
    }

    [Fact]
    public void SequentialAndPartitionedBenchmarksProduceMatchingValidationChecksums()
    {
        var workload = RadarProcessingSyntheticWorkload.Create(
            new RadarProcessingSyntheticWorkloadOptions(
                SourceCount: 6,
                BatchCount: 2,
                EventsPerBatch: 18,
                PayloadValuesPerEvent: 3));
        var benchmark = new RadarProcessingSyntheticBenchmark();

        var sequential = benchmark.Measure(
            workload,
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            iterations: 2,
            warmupIterations: 1,
            CancellationToken.None);
        var partitioned = benchmark.Measure(
            workload,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 6,
            shardCount: 3,
            RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            iterations: 2,
            warmupIterations: 1,
            CancellationToken.None);

        Assert.Equal(sequential.EventsPerIteration, partitioned.EventsPerIteration);
        Assert.Equal(sequential.PayloadValuesPerIteration, partitioned.PayloadValuesPerIteration);
        Assert.Equal(sequential.RawValueChecksumPerIteration, partitioned.RawValueChecksumPerIteration);
        Assert.Equal(sequential.ValidationChecksum, partitioned.ValidationChecksum);
    }

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

    [Fact]
    public void SyntheticBenchmarkRejectsInvalidInputs()
    {
        var benchmark = new RadarProcessingSyntheticBenchmark();
        var workload = RadarProcessingSyntheticWorkload.Create(
            new RadarProcessingSyntheticWorkloadOptions(
                SourceCount: 2,
                BatchCount: 1,
                EventsPerBatch: 2,
                PayloadValuesPerEvent: 1));

        Assert.Throws<ArgumentNullException>(() =>
            benchmark.Measure(
                (RadarProcessingSyntheticWorkload)null!,
                RadarProcessingExecutionMode.Sequential,
                partitionCount: 1,
                shardCount: 1,
                RadarProcessingBenchmarkHandlerSet.None,
                iterations: 1,
                warmupIterations: 0,
                CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            benchmark.Measure(
                workload,
                RadarProcessingExecutionMode.Sequential,
                partitionCount: 1,
                shardCount: 1,
                RadarProcessingBenchmarkHandlerSet.None,
                iterations: 0,
                warmupIterations: 0,
                CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingSyntheticWorkload.Create(
                new RadarProcessingSyntheticWorkloadOptions(
                    SourceCount: 0,
                    BatchCount: 1,
                    EventsPerBatch: 1,
                    PayloadValuesPerEvent: 1)));
    }
}
