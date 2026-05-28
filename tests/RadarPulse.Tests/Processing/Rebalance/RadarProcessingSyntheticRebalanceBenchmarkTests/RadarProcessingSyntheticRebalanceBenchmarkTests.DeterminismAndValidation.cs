using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmarkTests
{
    [Fact]
    public void BenchmarkTotalsAreDeterministicForSameWorkload()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm);

        var first = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 2,
            warmupIterations: 0);
        var second = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 2,
            warmupIterations: 0);

        Assert.Equal(first.TotalBatches, second.TotalBatches);
        Assert.Equal(first.TotalEvents, second.TotalEvents);
        Assert.Equal(first.RebalanceEvaluationCount, second.RebalanceEvaluationCount);
        Assert.Equal(first.AcceptedMoveCount, second.AcceptedMoveCount);
        Assert.Equal(first.SkippedDecisionCount, second.SkippedDecisionCount);
        Assert.Equal(first.DirectHotReliefCount, second.DirectHotReliefCount);
        Assert.Equal(first.ColdEvacuationCount, second.ColdEvacuationCount);
        Assert.Equal(first.ValidationChecksum, second.ValidationChecksum);
    }

    [Fact]
    public void AcceptedMovePressureAggregationDoesNotCopyPreviousIterations()
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 3_000,
            warmupIterations: 1);

        Assert.Equal(3_000, result.AcceptedMoveCount);
        Assert.Equal(3_000, result.AcceptedMovePressures.Count);
        Assert.True(
            result.AllocatedBytes < 250_000_000,
            $"Expected bounded benchmark aggregation allocation, got {result.AllocatedBytes} bytes.");
    }

    [Fact]
    public void BenchmarkRejectsInvalidInputs()
    {
        var benchmark = new RadarProcessingSyntheticRebalanceBenchmark();
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced);

        Assert.Throws<ArgumentNullException>(() =>
            benchmark.Measure(
                null!,
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            benchmark.Measure(
                workload,
                (RadarProcessingSyntheticRebalanceBenchmarkMode)255,
                iterations: 1,
                warmupIterations: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            benchmark.Measure(
                workload,
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 0,
                warmupIterations: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            benchmark.Measure(
                workload,
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            benchmark.Measure(
                workload,
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                iterations: 1,
                warmupIterations: 0,
                executionMode: (RadarProcessingExecutionMode)255));
    }
}
