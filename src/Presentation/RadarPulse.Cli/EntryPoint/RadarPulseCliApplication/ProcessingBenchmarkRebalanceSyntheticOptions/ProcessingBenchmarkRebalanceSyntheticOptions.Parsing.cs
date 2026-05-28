using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

public sealed partial record ProcessingBenchmarkRebalanceSyntheticOptions
{
    private static readonly IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> AllWorkloads =
        Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition,
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike,
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons,
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention
        ]);

    private static readonly IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> AllModes =
        Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession
        ]);

    private static IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> ParseWorkload(string value) =>
        value.ToLowerInvariant() switch
        {
            "all" => AllWorkloads,
            "balanced" => Single(RadarProcessingSyntheticRebalanceWorkloadKind.Balanced),
            "hot-shard" or "sustained-hot" or "sustained-hot-shard" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard),
            "intrinsic-hot" or "intrinsic-hot-partition" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition),
            "oscillating" or "oscillating-spike" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike),
            "cooldown" or "cooldown-storm" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm),
            "ttl-retry" or "quarantine-ttl-retry" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry),
            "cooling-clear" or "sustained-cooling-clear" or "quarantine-cooling-clear" or
                "quarantine-sustained-cooling-clear" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear),
            "pressure-change-retry" or "quarantine-pressure-change-retry" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry),
            "retry-reentry" or "quarantine-retry-reentry" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry),
            "successful-relief-clear" or "relief-clear" or "quarantine-successful-relief-clear" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear),
            "long-no-hot" or "long-no-hot-shard" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard),
            "long-cooldown" or "long-cooldown-rejection" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection),
            "long-unsafe-target" or "long-unsafe-target-rejection" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection),
            "long-mixed" or "long-mixed-skipped" or "long-mixed-skipped-reasons" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons),
            "counters-only" or "counters-only-retention" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention),
            _ => throw new ArgumentException($"Unknown synthetic rebalance workload: {value}")
        };

    private static IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> ParseMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "all" => AllModes,
            "static" or "static-no-rebalance" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance),
            "sampling" or "sampling-only" or "pressure-sampling" or "pressure-sampling-only" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly),
            "rebalance" or "session" or "rebalance-session" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession),
            "ordered" or "ordered-rebalance" or "ordered-rebalance-session" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession),
            _ => throw new ArgumentException($"Unknown synthetic rebalance benchmark mode: {value}")
        };

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sync" or "synchronous" or "partitioned" or "partitioned-barrier" =>
                RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown synthetic rebalance execution mode: {value}")
        };

    private static RadarProcessingValidationProfile ParseValidationProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "off" => RadarProcessingValidationProfile.Off,
            "essential" => RadarProcessingValidationProfile.Essential,
            "diagnostic" or "diagnostics" => RadarProcessingValidationProfile.Diagnostic,
            "benchmark" => RadarProcessingValidationProfile.Benchmark,
            _ => throw new ArgumentException($"Unknown synthetic rebalance validation profile: {value}")
        };

    private static IReadOnlyList<T> Single<T>(T value) => Array.AsReadOnly([value]);

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}
