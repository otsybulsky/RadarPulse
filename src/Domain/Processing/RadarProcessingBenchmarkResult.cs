namespace RadarPulse.Domain.Processing;

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
    long AllocatedBytes)
{
    public long TotalBatches => BatchesPerIteration * Iterations;

    public long TotalEvents => EventsPerIteration * Iterations;

    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;

    public double BatchesPerSecond => PerSecond(TotalBatches, Elapsed);

    public double EventsPerSecond => PerSecond(TotalEvents, Elapsed);

    public double PayloadValuesPerSecond => PerSecond(TotalPayloadValues, Elapsed);

    public double AllocatedBytesPerEvent => Ratio(AllocatedBytes, TotalEvents);

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
