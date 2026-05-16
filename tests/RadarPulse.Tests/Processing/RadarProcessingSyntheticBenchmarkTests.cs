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
