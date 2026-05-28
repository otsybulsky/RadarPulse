using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

internal static partial class ProcessingBenchmarkCliReporter
{
    public static string FormatProcessingMode(RadarProcessingExecutionMode executionMode) =>
        executionMode switch
        {
            RadarProcessingExecutionMode.Sequential => "sequential",
            RadarProcessingExecutionMode.PartitionedBarrier => "partitioned",
            RadarProcessingExecutionMode.AsyncShardTransport => "async",
            _ => executionMode.ToString()
        };

    public static string FormatProcessingHandlerSet(RadarProcessingBenchmarkHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarProcessingBenchmarkHandlerSet.None => "none",
            RadarProcessingBenchmarkHandlerSet.CounterChecksum => "counter-checksum",
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy => "counter-checksum-heavy",
            _ => handlerSet.ToString()
        };

    public static string FormatProcessingRebalanceWorkload(RadarProcessingSyntheticRebalanceWorkloadKind workloadKind) =>
        workloadKind switch
        {
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => "balanced",
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => "hot-shard",
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => "intrinsic-hot",
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => "oscillating",
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => "cooldown-storm",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry => "quarantine-ttl-retry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear =>
                "quarantine-cooling-clear",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry =>
                "quarantine-pressure-change-retry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry =>
                "quarantine-retry-reentry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear =>
                "quarantine-successful-relief-clear",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard => "long-no-hot-shard",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection => "long-cooldown-rejection",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection =>
                "long-unsafe-target-rejection",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons =>
                "long-mixed-skipped-reasons",
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention => "counters-only-retention",
            _ => workloadKind.ToString()
        };

    public static string FormatProcessingRebalanceMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode) =>
        mode switch
        {
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance => "static",
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly => "sampling",
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession => "rebalance-session",
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession => "ordered-rebalance-session",
            _ => mode.ToString()
        };

    public static string FormatProcessingValidationProfile(RadarProcessingValidationProfile profile) =>
        profile switch
        {
            RadarProcessingValidationProfile.Off => "off",
            RadarProcessingValidationProfile.Essential => "essential",
            RadarProcessingValidationProfile.Diagnostic => "diagnostic",
            RadarProcessingValidationProfile.Benchmark => "benchmark",
            _ => profile.ToString()
        };

    public static string FormatProcessingRetentionMode(RadarProcessingDiagnosticRetentionMode retentionMode) =>
        retentionMode switch
        {
            RadarProcessingDiagnosticRetentionMode.Counters => "counters",
            RadarProcessingDiagnosticRetentionMode.Recent => "recent",
            RadarProcessingDiagnosticRetentionMode.Diagnostic => "diagnostic",
            _ => retentionMode.ToString()
        };

    public static string FormatProcessingPressureSkewProfile(RadarProcessingPressureSkewProfile profile) =>
        profile switch
        {
            RadarProcessingPressureSkewProfile.None => "none",
            RadarProcessingPressureSkewProfile.HotShard => "hot-shard",
            RadarProcessingPressureSkewProfile.RotatingHotShard => "rotating-hot-shard",
            RadarProcessingPressureSkewProfile.HotPartition => "hot-partition",
            RadarProcessingPressureSkewProfile.TargetStarvation => "target-starvation",
            RadarProcessingPressureSkewProfile.BudgetStorm => "budget-storm",
            _ => profile.ToString()
        };
}
