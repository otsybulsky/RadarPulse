using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed partial record ProcessingBenchmarkArchiveRebalanceOptions
{
    private static ProcessingBenchmarkOptionValueSource CurrentDefaultOrExplicit(bool wasProvided) =>
        wasProvided
            ? ProcessingBenchmarkOptionValueSource.Explicit
            : ProcessingBenchmarkOptionValueSource.CurrentDefault;

    private static IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> ParseMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "all" => Array.AsReadOnly(
            [
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession
            ]),
            "static" or "static-no-rebalance" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance),
            "sampling" or "sampling-only" or "pressure-sampling" or "pressure-sampling-only" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly),
            "rebalance" or "session" or "rebalance-session" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession),
            _ => throw new ArgumentException($"Unknown archive rebalance benchmark mode: {value}")
        };

    private static RadarProcessingDiagnosticRetentionMode ParseRetentionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "counters" or "counter" or "counters-only" =>
                RadarProcessingDiagnosticRetentionMode.Counters,
            "recent" or "recent-detail" =>
                RadarProcessingDiagnosticRetentionMode.Recent,
            "diagnostic" or "diagnostics" =>
                RadarProcessingDiagnosticRetentionMode.Diagnostic,
            _ => throw new ArgumentException($"Unknown archive rebalance telemetry retention mode: {value}")
        };

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sync" or "synchronous" or "partitioned" or "partitioned-barrier" =>
                RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown archive rebalance execution mode: {value}")
        };

    private static RadarProcessingArchiveProviderMode ParseProviderMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "blocking" or "borrowed" or "blocking-borrowed" =>
                RadarProcessingArchiveProviderMode.BlockingBorrowed,
            "queued" or "owned" or "queued-owned" =>
                RadarProcessingArchiveProviderMode.QueuedOwned,
            _ => throw new ArgumentException($"Unknown archive rebalance provider mode: {value}")
        };

    private static RadarProcessingQueuedProviderOverlapMode ParseProviderOverlapMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => RadarProcessingQueuedProviderOverlapMode.None,
            "producer-consumer" or "producerconsumer" or "overlap" =>
                RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            _ => throw new ArgumentException($"Unknown archive rebalance provider overlap mode: {value}")
        };

    private static RadarProcessingRetainedPayloadStrategy ParseRetentionStrategy(string value) =>
        value.ToLowerInvariant() switch
        {
            "snapshot" or "snapshot-copy" =>
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            "pooled" or "pooled-copy" =>
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
            "builder" or "builder-transfer" =>
                RadarProcessingRetainedPayloadStrategy.BuilderTransfer,
            _ => throw new ArgumentException($"Unknown archive rebalance retention strategy: {value}")
        };

    private static ProcessingBenchmarkProviderQueueTelemetryOutput ParseQueueTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderQueueTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderQueueTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown archive rebalance queue telemetry mode: {value}")
        };

    private static ProcessingBenchmarkProviderOverlapTelemetryOutput ParseOverlapTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderOverlapTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown archive rebalance overlap telemetry mode: {value}")
        };

    private static RadarProcessingValidationProfile ParseValidationProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "off" => RadarProcessingValidationProfile.Off,
            "essential" => RadarProcessingValidationProfile.Essential,
            "diagnostic" or "diagnostics" => RadarProcessingValidationProfile.Diagnostic,
            "benchmark" => RadarProcessingValidationProfile.Benchmark,
            _ => throw new ArgumentException($"Unknown archive rebalance validation profile: {value}")
        };

    private static RadarProcessingPressureSkewProfile ParsePressureSkewProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => RadarProcessingPressureSkewProfile.None,
            "hot-shard" => RadarProcessingPressureSkewProfile.HotShard,
            "rotating-hot-shard" or "rotating-shard" =>
                RadarProcessingPressureSkewProfile.RotatingHotShard,
            "hot-partition" => RadarProcessingPressureSkewProfile.HotPartition,
            "target-starvation" or "no-cold-target" =>
                RadarProcessingPressureSkewProfile.TargetStarvation,
            "budget-storm" => RadarProcessingPressureSkewProfile.BudgetStorm,
            _ => throw new ArgumentException($"Unknown archive rebalance pressure skew profile: {value}")
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
