using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Measures deterministic synthetic rebalance scenarios across execution modes.
/// </summary>
public sealed partial class RadarProcessingSyntheticRebalanceBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;

    /// <summary>
    /// Synchronously measures a generated rebalance workload scenario.
    /// </summary>
    public RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkloadKind workloadKind,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1)
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(workloadKind);
        return Measure(
            workload,
            mode,
            iterations,
            warmupIterations,
            cancellationToken,
            hardeningOptions,
            executionMode,
            asyncExecution,
            orderedActiveBatchCapacity);
    }

    /// <summary>
    /// Synchronously measures an already-created rebalance workload.
    /// </summary>
    public RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1) =>
        MeasureAsync(
                workload,
                mode,
                iterations,
                warmupIterations,
                cancellationToken,
                hardeningOptions,
                executionMode,
                asyncExecution,
                orderedActiveBatchCapacity)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Asynchronously measures an already-created rebalance workload.
    /// </summary>
    public async ValueTask<RadarProcessingSyntheticRebalanceBenchmarkResult> MeasureAsync(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1)
    {
        ArgumentNullException.ThrowIfNull(workload);
        EnsureKnownMode(mode);
        EnsureKnownExecutionMode(executionMode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderedActiveBatchCapacity);
        var effectiveHardeningOptions = hardeningOptions ?? workload.HardeningOptions;
        var effectiveAsyncExecution = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? asyncExecution ?? new RadarProcessingAsyncExecutionOptions(workerCount: workload.ShardCount, queueCapacity: 1)
            : asyncExecution;

        var workerTelemetryRecorder = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingWorkerTelemetryRecorder(effectiveHardeningOptions.TelemetryRetention)
            : null;
        RadarProcessingAsyncWorkerGroup? workerGroup = null;
        try
        {
            workerGroup = executionMode == RadarProcessingExecutionMode.AsyncShardTransport
                ? new RadarProcessingAsyncWorkerGroup(
                    new RadarProcessingAsyncWorkerGroupOptions(effectiveAsyncExecution))
                : null;

            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunIterationAsync(
                    workload,
                    mode,
                    effectiveHardeningOptions,
                    executionMode,
                    effectiveAsyncExecution,
                    workerTelemetryRecorder: null,
                    workerGroup,
                    orderedActiveBatchCapacity,
                    cancellationToken).ConfigureAwait(false);
            }

            var allocationBefore = CaptureAllocationSnapshot(executionMode);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            IterationTelemetry? expectedIteration = null;
            var aggregate = IterationTelemetry.Empty;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationTelemetry = await RunIterationAsync(
                    workload,
                    mode,
                    effectiveHardeningOptions,
                    executionMode,
                    effectiveAsyncExecution,
                    workerTelemetryRecorder,
                    workerGroup,
                    orderedActiveBatchCapacity,
                    cancellationToken).ConfigureAwait(false);
                if (expectedIteration.HasValue && !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
                {
                    throw new InvalidDataException("Synthetic rebalance benchmark produced inconsistent iteration totals.");
                }

                expectedIteration ??= iterationTelemetry;
                aggregate = aggregate.Add(iterationTelemetry);
            }

            stopwatch.Stop();
            var allocatedBytes = CaptureAllocationSnapshot(executionMode).DeltaSince(allocationBefore);
            var allocationSummary = RadarProcessingRebalanceAllocationSummary.ForProcessingOnly(allocatedBytes);
            var measuredIteration = expectedIteration ??
                                    throw new InvalidOperationException("Synthetic rebalance benchmark did not run.");
            var workerTelemetry = workerTelemetryRecorder?.CreateSummary();
            if (workerTelemetry is not null)
            {
                var retentionValidation = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
                    workerTelemetry,
                    workerTelemetryRecorder!.Options,
                    effectiveHardeningOptions.ValidationProfile);
                if (!retentionValidation.IsValid)
                {
                    throw new InvalidDataException(retentionValidation.Message);
                }
            }

            return new RadarProcessingSyntheticRebalanceBenchmarkResult(
                workload.Kind,
                mode,
                iterations,
                warmupIterations,
                workload.SourceCount,
                workload.PartitionCount,
                workload.ShardCount,
                workload.BatchesPerIteration,
                workload.EventsPerIteration,
                workload.PayloadValuesPerIteration,
                workload.RawValueChecksumPerIteration,
                measuredIteration.TopologyVersionCount,
                aggregate.RebalanceEvaluationCount,
                aggregate.AcceptedMoveCount,
                aggregate.SkippedDecisionCount,
                aggregate.DirectHotReliefCount,
                aggregate.ColdEvacuationCount,
                aggregate.FailedMigrationCount,
                aggregate.ValidationSucceeded,
                aggregate.ValidationChecksum,
                CreateReadOnlyList(aggregate.SkippedReasons),
                CreateReadOnlyList(aggregate.AcceptedMovePressures),
                stopwatch.Elapsed,
                allocatedBytes,
                effectiveHardeningOptions.ValidationProfile,
                effectiveHardeningOptions.TelemetryRetention.RetentionMode,
                effectiveHardeningOptions.QuarantineLifecycle.QuarantineTtlEvaluations,
                effectiveHardeningOptions.QuarantineLifecycle.SustainedCoolingSampleCount,
                effectiveHardeningOptions.QuarantineLifecycle.MaterialPressureChangeThreshold,
                allocationSummary,
                executionMode,
                workerTelemetry,
                orderedActiveBatchCapacity);
        }
        finally
        {
            if (workerGroup is not null)
            {
                await workerGroup.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
