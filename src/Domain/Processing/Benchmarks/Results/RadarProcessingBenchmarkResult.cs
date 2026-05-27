namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports aggregate throughput, allocation, routing, validation, and worker telemetry from a processing benchmark.
/// </summary>
/// <param name="ExecutionMode">Execution mode measured by the benchmark.</param>
/// <param name="PartitionCount">Partition count used by the benchmark topology.</param>
/// <param name="ShardCount">Shard count used by the benchmark topology.</param>
/// <param name="HandlerSet">Handler set enabled during the benchmark.</param>
/// <param name="Iterations">Measured benchmark iteration count.</param>
/// <param name="WarmupIterations">Warmup iterations excluded from measured throughput.</param>
/// <param name="SourceCount">Source universe size used by the workload.</param>
/// <param name="BatchesPerIteration">Batches processed per measured iteration.</param>
/// <param name="EventsPerIteration">Events processed per measured iteration.</param>
/// <param name="PayloadValuesPerIteration">Payload values processed per measured iteration.</param>
/// <param name="RawValueChecksumPerIteration">Raw value checksum produced per measured iteration.</param>
/// <param name="ActiveSourceCount">Active sources after measured processing.</param>
/// <param name="ValidationChecksum">Deterministic processing checksum used for parity evidence.</param>
/// <param name="ShardDistributions">Per-shard event distribution for the routed workload.</param>
/// <param name="Elapsed">Elapsed measured benchmark time.</param>
/// <param name="AllocatedBytes">Allocated bytes attributed to the measured benchmark run.</param>
/// <param name="ValidationProfile">Validation profile used while producing benchmark evidence.</param>
/// <param name="WorkerTelemetry">Optional async worker telemetry captured during the run.</param>
/// <param name="AsyncValidation">Optional async validation evidence captured during the run.</param>
public sealed record RadarProcessingBenchmarkResult(
    RadarProcessingExecutionMode ExecutionMode,
    int PartitionCount,
    int ShardCount,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    int Iterations,
    int WarmupIterations,
    int SourceCount,
    long BatchesPerIteration,
    long EventsPerIteration,
    long PayloadValuesPerIteration,
    long RawValueChecksumPerIteration,
    long ActiveSourceCount,
    ulong ValidationChecksum,
    IReadOnlyList<RadarProcessingBenchmarkShardDistribution> ShardDistributions,
    TimeSpan Elapsed,
    long AllocatedBytes,
    RadarProcessingValidationProfile ValidationProfile = RadarProcessingValidationProfile.Benchmark,
    RadarProcessingWorkerTelemetrySummary? WorkerTelemetry = null,
    RadarProcessingAsyncValidationResult? AsyncValidation = null)
{
    /// <summary>
    /// Gets whether async worker telemetry is present.
    /// </summary>
    public bool HasWorkerTelemetry => WorkerTelemetry is not null;

    /// <summary>
    /// Gets total measured batches across all iterations.
    /// </summary>
    public long TotalBatches => BatchesPerIteration * Iterations;

    /// <summary>
    /// Gets total measured events across all iterations.
    /// </summary>
    public long TotalEvents => EventsPerIteration * Iterations;

    /// <summary>
    /// Gets total measured payload values across all iterations.
    /// </summary>
    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;

    /// <summary>
    /// Gets measured batch throughput.
    /// </summary>
    public double BatchesPerSecond => PerSecond(TotalBatches, Elapsed);

    /// <summary>
    /// Gets measured event throughput.
    /// </summary>
    public double EventsPerSecond => PerSecond(TotalEvents, Elapsed);

    /// <summary>
    /// Gets measured payload value throughput.
    /// </summary>
    public double PayloadValuesPerSecond => PerSecond(TotalPayloadValues, Elapsed);

    /// <summary>
    /// Gets allocated bytes per processed event.
    /// </summary>
    public double AllocatedBytesPerEvent => Ratio(AllocatedBytes, TotalEvents);

    /// <summary>
    /// Gets allocated bytes per processed payload value.
    /// </summary>
    public double AllocatedBytesPerPayloadValue => Ratio(AllocatedBytes, TotalPayloadValues);

    private static double PerSecond(
        long value,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : value / elapsed.TotalSeconds;

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
