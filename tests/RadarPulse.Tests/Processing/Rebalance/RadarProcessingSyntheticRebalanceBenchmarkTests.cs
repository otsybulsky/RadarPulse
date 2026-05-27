using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingSyntheticRebalanceBenchmarkTests
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

    [Fact]
    public void BenchmarkResultIncludesAllocationAttributionAndProfiles()
    {
        var hardeningOptions = new RadarProcessingRebalanceHardeningOptions(
            telemetryRetention: new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Counters),
            validationProfile: RadarProcessingValidationProfile.Benchmark);

        var result = new RadarProcessingSyntheticRebalanceBenchmark().Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 1,
            warmupIterations: 0,
            hardeningOptions: hardeningOptions);

        Assert.Equal(RadarProcessingValidationProfile.Benchmark, result.ValidationProfile);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Counters, result.RetentionMode);
        Assert.True(result.AllocationSummary.IsMeasured);
        Assert.False(result.AllocationSummary.IncludesArchiveReplayAndBatchConstruction);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
        Assert.Equal(result.AllocatedBytes, result.AllocationSummary.MeasuredAllocatedBytes);
        Assert.Equal(result.AllocatedBytes, result.ProcessingCallbackAllocatedBytes);
        Assert.Equal(result.AllocatedBytesPerPayloadValue, result.ProcessingCallbackAllocatedBytesPerPayloadValue);
    }

    [Theory]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance)]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly)]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession)]
    public void SameRunModesPopulateComparableAllocationFields(
        RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            mode);

        Assert.True(result.AllocationSummary.IsMeasured);
        Assert.Equal(RadarProcessingValidationProfile.Diagnostic, result.ValidationProfile);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Recent, result.RetentionMode);
        Assert.Equal(result.AllocatedBytes, result.AllocationSummary.MeasuredAllocatedBytes);
        Assert.Equal(result.AllocatedBytes, result.AllocationSummary.ProcessingCallbackAllocatedBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    [Fact]
    public void DirectReliefBenchmarkRecordsAcceptedDirectMoves()
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession);

        Assert.True(result.AcceptedMoveCount >= 1);
        Assert.True(result.DirectHotReliefCount >= 1);
        Assert.Equal(0, result.ColdEvacuationCount);
        Assert.NotEmpty(result.AcceptedMovePressures);
        Assert.All(
            result.AcceptedMovePressures,
            pressure => Assert.True(pressure.ExpectedRelief > 0));
        Assert.True(result.ValidationSucceeded);
    }

    [Fact]
    public void ColdEvacuationBenchmarkRecordsAcceptedColdMoves()
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession);

        Assert.Equal(1, result.AcceptedMoveCount);
        Assert.Equal(0, result.DirectHotReliefCount);
        Assert.Equal(1, result.ColdEvacuationCount);
        Assert.Contains(
            result.AcceptedMovePressures,
            pressure => pressure.MoveKind == Domain.Processing.RadarProcessingRebalanceMoveKind.ColdEvacuation);
        Assert.True(result.SkippedReasons.Count > 0);
        Assert.True(result.ValidationSucceeded);
    }

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

    private static RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkloadKind workloadKind,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations = 1,
        int warmupIterations = 0) =>
        new RadarProcessingSyntheticRebalanceBenchmark().Measure(
            workloadKind,
            mode,
            iterations,
            warmupIterations);

    private static RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1) =>
        new RadarProcessingSyntheticRebalanceBenchmark().Measure(
            workload,
            mode,
            iterations,
            warmupIterations,
            executionMode: executionMode,
            asyncExecution: asyncExecution,
            orderedActiveBatchCapacity: orderedActiveBatchCapacity);
}
