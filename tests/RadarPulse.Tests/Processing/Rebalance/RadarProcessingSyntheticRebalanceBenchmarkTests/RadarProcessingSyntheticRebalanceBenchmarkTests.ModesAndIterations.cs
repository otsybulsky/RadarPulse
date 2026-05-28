using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmarkTests
{
    [Fact]
    public void WarmupIterationsAreExcludedFromMeasuredTotals()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard);
        var single = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 1,
            warmupIterations: 0);

        var measured = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 2,
            warmupIterations: 1);

        Assert.Equal(2, measured.Iterations);
        Assert.Equal(1, measured.WarmupIterations);
        Assert.Equal(single.AcceptedMoveCount * 2, measured.AcceptedMoveCount);
        Assert.Equal(single.RebalanceEvaluationCount * 2, measured.RebalanceEvaluationCount);
        Assert.Equal(workload.EventsPerIteration * 2, measured.TotalEvents);
        Assert.Equal(workload.PayloadValuesPerIteration * 2, measured.TotalPayloadValues);
    }

    [Fact]
    public void StaticBaselinePerformsZeroRebalanceEvaluations()
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance);

        Assert.Equal(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance, result.Mode);
        Assert.Equal(0, result.RebalanceEvaluationCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(0, result.SkippedDecisionCount);
        Assert.Equal(1, result.TopologyVersionCount);
        Assert.True(result.ValidationSucceeded);
        Assert.NotEqual(0UL, result.ValidationChecksum);
    }

    [Fact]
    public void SamplingOnlyModeRecordsEvaluationsWithZeroMoves()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard);

        var result = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            iterations: 1,
            warmupIterations: 0);

        Assert.Equal(workload.BatchesPerIteration, result.RebalanceEvaluationCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.Equal(0, result.DirectHotReliefCount);
        Assert.Equal(0, result.ColdEvacuationCount);
        Assert.Equal(0, result.FailedMigrationCount);
        Assert.True(result.ValidationSucceeded);
    }

    [Fact]
    public void AsyncStaticBaselineReportsMeasuredWorkerTelemetry()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced);

        var result = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
            iterations: 2,
            warmupIterations: 1,
            executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1));

        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ExecutionMode);
        Assert.True(result.HasWorkerTelemetry);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(2, result.WorkerTelemetry.WorkerCount);
        Assert.Equal(1, result.WorkerTelemetry.QueueCapacity);
        Assert.Equal(workload.BatchesPerIteration * result.Iterations, result.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(0, result.RebalanceEvaluationCount);
        Assert.True(result.ValidationSucceeded);
    }

    [Fact]
    public void AsyncSamplingModeRecordsWorkerTelemetryAndEvaluations()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard);

        var result = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            iterations: 1,
            warmupIterations: 1,
            executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1));

        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ExecutionMode);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(workload.BatchesPerIteration, result.RebalanceEvaluationCount);
        Assert.Equal(workload.BatchesPerIteration, result.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(0, result.AcceptedMoveCount);
        Assert.True(result.ValidationSucceeded);
    }
}
