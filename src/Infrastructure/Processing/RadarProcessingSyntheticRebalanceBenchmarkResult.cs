using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingSyntheticRebalanceBenchmarkResult(
    RadarProcessingSyntheticRebalanceWorkloadKind WorkloadKind,
    RadarProcessingSyntheticRebalanceBenchmarkMode Mode,
    int Iterations,
    int WarmupIterations,
    int SourceCount,
    int PartitionCount,
    int ShardCount,
    long BatchesPerIteration,
    long EventsPerIteration,
    long PayloadValuesPerIteration,
    long RawValueChecksumPerIteration,
    long TopologyVersionCount,
    long RebalanceEvaluationCount,
    long AcceptedMoveCount,
    long SkippedDecisionCount,
    long DirectHotReliefCount,
    long ColdEvacuationCount,
    long FailedMigrationCount,
    bool ValidationSucceeded,
    ulong ValidationChecksum,
    IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons,
    IReadOnlyList<RadarProcessingSyntheticRebalanceMovePressure> AcceptedMovePressures,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    public long TotalBatches => BatchesPerIteration * Iterations;

    public long TotalEvents => EventsPerIteration * Iterations;

    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;

    public double BatchesPerSecond => PerSecond(TotalBatches, Elapsed);

    public double EventsPerSecond => PerSecond(TotalEvents, Elapsed);

    public double PayloadValuesPerSecond => PerSecond(TotalPayloadValues, Elapsed);

    public double RebalanceEvaluationsPerSecond => PerSecond(RebalanceEvaluationCount, Elapsed);

    public double AllocatedBytesPerStreamEvent => Ratio(AllocatedBytes, TotalEvents);

    public double AllocatedBytesPerRebalanceEvaluation => Ratio(AllocatedBytes, RebalanceEvaluationCount);

    private static double PerSecond(
        long value,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : value / elapsed.TotalSeconds;

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
