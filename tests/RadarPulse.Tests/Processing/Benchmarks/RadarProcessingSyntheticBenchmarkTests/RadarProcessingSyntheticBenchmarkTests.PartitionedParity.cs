using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticBenchmarkTests
{
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
}
