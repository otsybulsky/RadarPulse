using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmarkTests
{
    [Fact]
    public void AsyncRebalanceMatchesSynchronousDeterministicTotals()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard);

        var synchronous = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 1,
            warmupIterations: 0);
        var asynchronous = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 1,
            warmupIterations: 0,
            executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1));

        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, synchronous.ExecutionMode);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, asynchronous.ExecutionMode);
        Assert.NotNull(asynchronous.WorkerTelemetry);
        Assert.Equal(synchronous.TotalBatches, asynchronous.TotalBatches);
        Assert.Equal(synchronous.TotalEvents, asynchronous.TotalEvents);
        Assert.Equal(synchronous.RebalanceEvaluationCount, asynchronous.RebalanceEvaluationCount);
        Assert.Equal(synchronous.AcceptedMoveCount, asynchronous.AcceptedMoveCount);
        Assert.Equal(synchronous.SkippedDecisionCount, asynchronous.SkippedDecisionCount);
        Assert.Equal(synchronous.DirectHotReliefCount, asynchronous.DirectHotReliefCount);
        Assert.Equal(synchronous.ColdEvacuationCount, asynchronous.ColdEvacuationCount);
        Assert.Equal(synchronous.ValidationChecksum, asynchronous.ValidationChecksum);
        Assert.True(asynchronous.ValidationSucceeded);
    }

    [Fact]
    public void OrderedRebalanceMatchesSequentialDeterministicTotals()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard);

        var sequential = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 1,
            warmupIterations: 0);
        var ordered = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession,
            iterations: 1,
            warmupIterations: 0,
            orderedActiveBatchCapacity: 2);

        Assert.Equal(2, ordered.OrderedActiveBatchCapacity);
        Assert.Equal(sequential.TotalBatches, ordered.TotalBatches);
        Assert.Equal(sequential.TotalEvents, ordered.TotalEvents);
        Assert.Equal(sequential.TotalPayloadValues, ordered.TotalPayloadValues);
        Assert.Equal(sequential.TopologyVersionCount, ordered.TopologyVersionCount);
        Assert.Equal(sequential.RebalanceEvaluationCount, ordered.RebalanceEvaluationCount);
        Assert.Equal(sequential.AcceptedMoveCount, ordered.AcceptedMoveCount);
        Assert.Equal(sequential.SkippedDecisionCount, ordered.SkippedDecisionCount);
        Assert.Equal(sequential.DirectHotReliefCount, ordered.DirectHotReliefCount);
        Assert.Equal(sequential.ColdEvacuationCount, ordered.ColdEvacuationCount);
        Assert.Equal(sequential.ValidationChecksum, ordered.ValidationChecksum);
        Assert.True(ordered.ValidationSucceeded);
    }

    [Fact]
    public void AsyncOrderedRebalanceRecordsWorkerTelemetry()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced);

        var result = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession,
            iterations: 1,
            warmupIterations: 0,
            executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 4),
            orderedActiveBatchCapacity: 2);

        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, result.ExecutionMode);
        Assert.Equal(2, result.OrderedActiveBatchCapacity);
        Assert.True(result.HasWorkerTelemetry);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.Equal(2, result.WorkerTelemetry.WorkerCount);
        Assert.Equal(4, result.WorkerTelemetry.QueueCapacity);
        Assert.Equal(workload.BatchesPerIteration, result.WorkerTelemetry.Counters.CompletedBatchCount);
        Assert.Equal(workload.BatchesPerIteration, result.RebalanceEvaluationCount);
        Assert.True(result.ValidationSucceeded);
    }

    [Fact]
    public void AsyncOrderedRebalanceSupportsActiveCapacityAboveWorkerCount()
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons);

        var result = Measure(
            workload,
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession,
            iterations: 1,
            warmupIterations: 0,
            executionMode: RadarProcessingExecutionMode.AsyncShardTransport,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 8),
            orderedActiveBatchCapacity: 4);

        Assert.True(result.ValidationSucceeded);
        Assert.NotNull(result.WorkerTelemetry);
        Assert.True(result.WorkerTelemetry.Counters.CompletedBatchCount >= workload.BatchesPerIteration);
        Assert.Equal(0, result.WorkerTelemetry.Counters.FailedBatchCount);
        Assert.Equal(0, result.WorkerTelemetry.Counters.FailedWorkItemCount);
    }
}
