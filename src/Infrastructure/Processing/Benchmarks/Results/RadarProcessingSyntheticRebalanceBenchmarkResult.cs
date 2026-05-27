using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Throughput, allocation, validation, and rebalance evidence from a synthetic rebalance benchmark.
/// </summary>
public sealed record RadarProcessingSyntheticRebalanceBenchmarkResult(
    /// <summary>
    /// Scenario measured by the benchmark.
    /// </summary>
    RadarProcessingSyntheticRebalanceWorkloadKind WorkloadKind,
    /// <summary>
    /// Benchmark processing mode.
    /// </summary>
    RadarProcessingSyntheticRebalanceBenchmarkMode Mode,
    /// <summary>
    /// Number of measured iterations.
    /// </summary>
    int Iterations,
    /// <summary>
    /// Number of warmup iterations excluded from measurements.
    /// </summary>
    int WarmupIterations,
    /// <summary>
    /// Source count in the workload.
    /// </summary>
    int SourceCount,
    /// <summary>
    /// Partition count in the workload.
    /// </summary>
    int PartitionCount,
    /// <summary>
    /// Shard count in the workload.
    /// </summary>
    int ShardCount,
    /// <summary>
    /// Batch count per iteration.
    /// </summary>
    long BatchesPerIteration,
    /// <summary>
    /// Event count per iteration.
    /// </summary>
    long EventsPerIteration,
    /// <summary>
    /// Payload value count per iteration.
    /// </summary>
    long PayloadValuesPerIteration,
    /// <summary>
    /// Raw value checksum per iteration.
    /// </summary>
    long RawValueChecksumPerIteration,
    /// <summary>
    /// Topology versions observed in a measured iteration.
    /// </summary>
    long TopologyVersionCount,
    /// <summary>
    /// Rebalance evaluations performed across measured iterations.
    /// </summary>
    long RebalanceEvaluationCount,
    /// <summary>
    /// Accepted rebalance move count across measured iterations.
    /// </summary>
    long AcceptedMoveCount,
    /// <summary>
    /// Skipped rebalance decision count across measured iterations.
    /// </summary>
    long SkippedDecisionCount,
    /// <summary>
    /// Direct hot-relief decision count across measured iterations.
    /// </summary>
    long DirectHotReliefCount,
    /// <summary>
    /// Cold evacuation decision count across measured iterations.
    /// </summary>
    long ColdEvacuationCount,
    /// <summary>
    /// Failed migration count across measured iterations.
    /// </summary>
    long FailedMigrationCount,
    /// <summary>
    /// Indicates whether benchmark validation succeeded.
    /// </summary>
    bool ValidationSucceeded,
    /// <summary>
    /// Deterministic validation checksum.
    /// </summary>
    ulong ValidationChecksum,
    /// <summary>
    /// Distinct skipped rebalance reasons observed.
    /// </summary>
    IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons,
    /// <summary>
    /// Pressure evidence for accepted moves.
    /// </summary>
    IReadOnlyList<RadarProcessingSyntheticRebalanceMovePressure> AcceptedMovePressures,
    /// <summary>
    /// Measured elapsed time.
    /// </summary>
    TimeSpan Elapsed,
    /// <summary>
    /// Allocated bytes measured for the benchmark.
    /// </summary>
    long AllocatedBytes,
    /// <summary>
    /// Validation profile used by the benchmark.
    /// </summary>
    RadarProcessingValidationProfile ValidationProfile = RadarProcessingValidationProfile.Diagnostic,
    /// <summary>
    /// Diagnostic retention mode used by telemetry.
    /// </summary>
    RadarProcessingDiagnosticRetentionMode RetentionMode = RadarProcessingDiagnosticRetentionMode.Recent,
    /// <summary>
    /// Quarantine TTL setting used by the benchmark.
    /// </summary>
    int QuarantineTtlEvaluations = 64,
    /// <summary>
    /// Sustained cooling sample count used by quarantine lifecycle.
    /// </summary>
    int QuarantineSustainedCoolingSampleCount = 3,
    /// <summary>
    /// Material pressure-change threshold used by quarantine lifecycle.
    /// </summary>
    double QuarantineMaterialPressureChangeThreshold = 0.25,
    /// <summary>
    /// Allocation breakdown for processing callbacks.
    /// </summary>
    RadarProcessingRebalanceAllocationSummary AllocationSummary = default,
    /// <summary>
    /// Execution mode measured by the benchmark.
    /// </summary>
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    /// <summary>
    /// Worker telemetry when async shard transport is measured.
    /// </summary>
    RadarProcessingWorkerTelemetrySummary? WorkerTelemetry = null,
    /// <summary>
    /// Ordered active batch capacity used by ordered concurrent mode.
    /// </summary>
    int OrderedActiveBatchCapacity = 1)
{
    /// <summary>
    /// Indicates whether async worker telemetry was captured.
    /// </summary>
    public bool HasWorkerTelemetry => WorkerTelemetry is not null;

    /// <summary>
    /// Total batches processed across measured iterations.
    /// </summary>
    public long TotalBatches => BatchesPerIteration * Iterations;

    /// <summary>
    /// Total events processed across measured iterations.
    /// </summary>
    public long TotalEvents => EventsPerIteration * Iterations;

    /// <summary>
    /// Total payload values processed across measured iterations.
    /// </summary>
    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;

    /// <summary>
    /// Batch throughput over measured elapsed time.
    /// </summary>
    public double BatchesPerSecond => PerSecond(TotalBatches, Elapsed);

    /// <summary>
    /// Event throughput over measured elapsed time.
    /// </summary>
    public double EventsPerSecond => PerSecond(TotalEvents, Elapsed);

    /// <summary>
    /// Payload value throughput over measured elapsed time.
    /// </summary>
    public double PayloadValuesPerSecond => PerSecond(TotalPayloadValues, Elapsed);

    /// <summary>
    /// Rebalance evaluation throughput over measured elapsed time.
    /// </summary>
    public double RebalanceEvaluationsPerSecond => PerSecond(RebalanceEvaluationCount, Elapsed);

    /// <summary>
    /// Allocated bytes divided by total processed events.
    /// </summary>
    public double AllocatedBytesPerStreamEvent => Ratio(AllocatedBytes, TotalEvents);

    /// <summary>
    /// Allocated bytes divided by total processed payload values.
    /// </summary>
    public double AllocatedBytesPerPayloadValue => Ratio(AllocatedBytes, TotalPayloadValues);

    /// <summary>
    /// Allocated bytes divided by rebalance evaluation count.
    /// </summary>
    public double AllocatedBytesPerRebalanceEvaluation => Ratio(AllocatedBytes, RebalanceEvaluationCount);

    /// <summary>
    /// Allocation bytes attributed to processing callbacks.
    /// </summary>
    public long ProcessingCallbackAllocatedBytes => AllocationSummary.ProcessingCallbackAllocatedBytes;

    /// <summary>
    /// Processing callback allocated bytes divided by total payload values.
    /// </summary>
    public double ProcessingCallbackAllocatedBytesPerPayloadValue =>
        AllocationSummary.ProcessingCallbackAllocatedBytesPerPayloadValue(TotalPayloadValues);

    /// <summary>
    /// Processing callback allocated bytes divided by rebalance evaluations.
    /// </summary>
    public double ProcessingCallbackAllocatedBytesPerRebalanceEvaluation =>
        AllocationSummary.ProcessingCallbackAllocatedBytesPerRebalanceEvaluation(RebalanceEvaluationCount);

    private static double PerSecond(
        long value,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : value / elapsed.TotalSeconds;

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
