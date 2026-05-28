using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticBenchmarkTests
{
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
