using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed record ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
    int? QuarantineTtlEvaluations,
    int? SustainedCoolingSampleCount,
    double? MaterialPressureChangeThreshold)
{
    /// <summary>
    /// Gets an override set with no overridden lifecycle values.
    /// </summary>
    public static ProcessingBenchmarkQuarantineLifecycleOptionOverrides None { get; } = new(null, null, null);

    /// <summary>
    /// Gets whether at least one lifecycle option was explicitly supplied.
    /// </summary>
    public bool HasOverrides =>
        QuarantineTtlEvaluations is not null ||
        SustainedCoolingSampleCount is not null ||
        MaterialPressureChangeThreshold is not null;

    /// <summary>
    /// Applies configured overrides to a baseline lifecycle option set.
    /// </summary>
    public RadarProcessingQuarantineLifecycleOptions ApplyTo(
        RadarProcessingQuarantineLifecycleOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (!HasOverrides)
        {
            return baseline;
        }

        return new RadarProcessingQuarantineLifecycleOptions(
            QuarantineTtlEvaluations ?? baseline.QuarantineTtlEvaluations,
            SustainedCoolingSampleCount ?? baseline.SustainedCoolingSampleCount,
            MaterialPressureChangeThreshold ?? baseline.MaterialPressureChangeThreshold);
    }
}

/// <summary>
/// CLI options for synthetic processing rebalance benchmark runs.
/// </summary>
public sealed record ProcessingBenchmarkRebalanceSyntheticOptions(
    IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> Workloads,
    IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> Modes,
    RadarProcessingValidationProfile ValidationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides QuarantineLifecycleOverrides,
    int Iterations,
    int WarmupIterations,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingAsyncExecutionOptions? AsyncExecution = null,
    int OrderedActiveBatchCapacityValue = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity)
{
    private const int DefaultAsyncWorkerCount = 2;
    private const int DefaultAsyncQueueCapacity = 1;

    /// <summary>
    /// Gets the active batch capacity used by ordered rebalance sessions.
    /// </summary>
    public int OrderedActiveBatchCapacity => OrderedActiveBatchCapacityValue;

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

    /// <summary>
    /// Parses synthetic rebalance benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkRebalanceSyntheticOptions Parse(string[] args)
    {
        IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> workloads = Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced
        ]);
        IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> modes = AllModes;
        var validationProfile = RadarProcessingValidationProfile.Diagnostic;
        int? quarantineTtlEvaluations = null;
        int? sustainedCoolingSampleCount = null;
        double? materialPressureChangeThreshold = null;
        var iterations = 3;
        var warmupIterations = 1;
        var executionMode = RadarProcessingExecutionMode.PartitionedBarrier;
        int? workerCount = null;
        int? queueCapacity = null;
        var orderedActiveBatchCapacity = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--workload":
                    workloads = ParseWorkload(RequireValue(args, ref i, "--workload"));
                    break;
                case "--mode":
                    modes = ParseMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--execution":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--execution"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    break;
                case "--active-batches":
                case "--ordered-active-batches":
                    orderedActiveBatchCapacity = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--validation-profile":
                    validationProfile = ParseValidationProfile(RequireValue(args, ref i, "--validation-profile"));
                    break;
                case "--quarantine-ttl":
                case "--quarantine-ttl-evaluations":
                    quarantineTtlEvaluations = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-sustained-cooling-samples":
                case "--quarantine-sustained-cooling-sample-count":
                    sustainedCoolingSampleCount = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-material-pressure-change":
                case "--quarantine-material-pressure-change-threshold":
                    materialPressureChangeThreshold = double.Parse(
                        RequireValue(args, ref i, args[i]),
                        CultureInfo.InvariantCulture);
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (workerCount.HasValue && workerCount.Value <= 0)
        {
            throw new InvalidOperationException("--workers must be greater than zero.");
        }

        if (queueCapacity.HasValue && queueCapacity.Value <= 0)
        {
            throw new InvalidOperationException("--queue-capacity must be greater than zero.");
        }

        if (orderedActiveBatchCapacity <= 0)
        {
            throw new InvalidOperationException("--active-batches must be greater than zero.");
        }

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? DefaultAsyncWorkerCount,
                queueCapacity: queueCapacity ?? DefaultAsyncQueueCapacity);
        }
        else if (workerCount.HasValue || queueCapacity.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --execution async.");
        }

        var quarantineLifecycleOverrides = new ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
            quarantineTtlEvaluations,
            sustainedCoolingSampleCount,
            materialPressureChangeThreshold);
        _ = quarantineLifecycleOverrides.ApplyTo(RadarProcessingQuarantineLifecycleOptions.Default);

        return new ProcessingBenchmarkRebalanceSyntheticOptions(
            workloads,
            modes,
            validationProfile,
            quarantineLifecycleOverrides,
            iterations,
            warmupIterations,
            executionMode,
            asyncExecution,
            orderedActiveBatchCapacity);
    }

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

/// <summary>
/// CLI options for archive-backed processing rebalance benchmark runs.
