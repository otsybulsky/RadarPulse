using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

internal sealed record ArchiveOptions(
    DateOnly? Date,
    IReadOnlyCollection<string> RadarIds,
    bool AllRadars,
    int? MaxFiles,
    long? MaxBytes,
    string? ManifestPath,
    string? OutputPath,
    int Concurrency)
{
    /// <summary>
    /// Parses historical archive download options from CLI arguments.
    /// </summary>
    public static ArchiveOptions Parse(string[] args)
    {
        DateOnly? date = null;
        var radarIds = new List<string>();
        var allRadars = false;
        int? maxFiles = null;
        long? maxBytes = null;
        string? manifestPath = null;
        string? outputPath = null;
        var concurrency = 4;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarIds.Add(HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar")));
                    break;
                case "--all-radars":
                    allRadars = true;
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                case "--max-bytes":
                    maxBytes = long.Parse(RequireValue(args, ref i, "--max-bytes"));
                    break;
                case "--manifest":
                    manifestPath = RequireValue(args, ref i, "--manifest");
                    break;
                case "--output":
                    outputPath = RequireValue(args, ref i, "--output");
                    break;
                case "--concurrency":
                    concurrency = int.Parse(RequireValue(args, ref i, "--concurrency"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (concurrency <= 0)
        {
            throw new InvalidOperationException("--concurrency must be greater than zero.");
        }

        if (maxFiles is <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (maxBytes is <= 0)
        {
            throw new InvalidOperationException("--max-bytes must be greater than zero.");
        }

        return new ArchiveOptions(date, radarIds, allRadars, maxFiles, maxBytes, manifestPath, outputPath, concurrency);
    }

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

internal sealed record ArchiveInspectOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles)
{
    /// <summary>
    /// Parses Archive II inspection options from CLI arguments.
    /// </summary>
    public static ArchiveInspectOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null))
        {
            throw new InvalidOperationException("--date and --radar can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        return new ArchiveInspectOptions(filePath, cachePath, date, radarId, maxFiles);
    }

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

internal sealed record ProcessingBenchmarkSyntheticOptions(
    RadarProcessingExecutionMode ExecutionMode,
    int SourceCount,
    int BatchCount,
    int EventsPerBatch,
    int PayloadValuesPerEvent,
    int PartitionCount,
    int ShardCount,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    int Iterations,
    int WarmupIterations,
    RadarProcessingAsyncExecutionOptions? AsyncExecution)
{
    /// <summary>
    /// Parses synthetic processing benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkSyntheticOptions Parse(string[] args)
    {
        var executionMode = RadarProcessingExecutionMode.Sequential;
        var sourceCount = 16;
        var batchCount = 4;
        var eventsPerBatch = 1024;
        var payloadValuesPerEvent = 4;
        var partitionCount = 1;
        var shardCount = 1;
        var handlerSet = RadarProcessingBenchmarkHandlerSet.None;
        var iterations = 3;
        var warmupIterations = 1;
        int? workerCount = null;
        int? queueCapacity = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--sources":
                    sourceCount = int.Parse(RequireValue(args, ref i, "--sources"));
                    break;
                case "--batches":
                    batchCount = int.Parse(RequireValue(args, ref i, "--batches"));
                    break;
                case "--events-per-batch":
                    eventsPerBatch = int.Parse(RequireValue(args, ref i, "--events-per-batch"));
                    break;
                case "--payload-values":
                    payloadValuesPerEvent = int.Parse(RequireValue(args, ref i, "--payload-values"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    break;
                case "--handlers":
                    handlerSet = ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
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

        new RadarProcessingSyntheticWorkloadOptions(
            sourceCount,
            batchCount,
            eventsPerBatch,
            payloadValuesPerEvent).Validate();

        if (partitionCount <= 0)
        {
            throw new InvalidOperationException("--partitions must be greater than zero.");
        }

        if (shardCount <= 0)
        {
            throw new InvalidOperationException("--shards must be greater than zero.");
        }

        if (partitionCount < shardCount)
        {
            throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
        }

        if (partitionCount > sourceCount)
        {
            throw new InvalidOperationException("--partitions must be less than or equal to --sources.");
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

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? shardCount,
                queueCapacity: queueCapacity ?? 1);
        }
        else if (workerCount.HasValue || queueCapacity.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --mode async.");
        }

        return new ProcessingBenchmarkSyntheticOptions(
            executionMode,
            sourceCount,
            batchCount,
            eventsPerBatch,
            payloadValuesPerEvent,
            partitionCount,
            shardCount,
            handlerSet,
            iterations,
            warmupIterations,
            asyncExecution);
    }

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sequential" => RadarProcessingExecutionMode.Sequential,
            "partitioned" or "partitioned-barrier" => RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown processing mode: {value}")
        };

    private static RadarProcessingBenchmarkHandlerSet ParseHandlerSet(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => RadarProcessingBenchmarkHandlerSet.None,
            "counter-checksum" => RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            "counter-checksum-heavy" or "counter-checksum+heavy" or "standard-heavy" =>
                RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy,
            _ => throw new ArgumentException($"Unknown processing benchmark handler set: {value}")
        };

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
/// Optional CLI overrides for quarantine lifecycle settings used by processing benchmark commands.
/// </summary>
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
/// </summary>
public sealed record ProcessingBenchmarkArchiveRebalanceOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> Modes,
    int PartitionCount,
    int ShardCount,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    RadarProcessingValidationProfile ValidationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides QuarantineLifecycleOverrides,
    RadarProcessingTelemetryRetentionOptions TelemetryRetention,
    RadarProcessingPressureSkewOptions PressureSkew,
    RadarProcessingArchiveProviderMode ProviderMode = RadarProcessingArchiveProviderMode.BlockingBorrowed,
    int ProviderQueueCapacity = 1,
    TimeSpan? ProviderQueueTimeout = null,
    RadarProcessingQueuedProviderOverlapMode ProviderOverlapMode = RadarProcessingQueuedProviderOverlapMode.None,
    RadarProcessingRetainedPayloadStrategy RetentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
    long? ProviderQueueRetainedPayloadBytes = null,
    TimeSpan OverlapConsumerDelay = default,
    ProcessingBenchmarkProviderQueueTelemetryOutput QueueTelemetryOutput =
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
    ProcessingBenchmarkProviderOverlapTelemetryOutput OverlapTelemetryOutput =
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingAsyncExecutionOptions? AsyncExecution = null,
    ProcessingBenchmarkArchiveRebalanceOptionProvenance? OptionProvenance = null)
{
    public const int DefaultCandidateProviderQueueCapacity =
        RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity;
    public const long DefaultCandidateRetainedPayloadBytes =
        RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes;
    public const int DefaultRolloutWorkerCount = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount;
    public const int DefaultRolloutProviderQueueCapacity = DefaultCandidateProviderQueueCapacity;
    public const long DefaultRolloutRetainedPayloadBytes = DefaultCandidateRetainedPayloadBytes;
    public const string NaturalDefaultCandidateEvidenceContour = "natural-default-candidate";
    public const string ControlledProofEvidenceContour = "controlled-proof";
    public const string NaturalOptInEvidenceContour = "natural-opt-in";
    public const string NotApplicableEvidenceContour = "not-applicable";
    public const string NaturalReadinessEvidenceScope = "natural-readiness";
    public const string ControlledMechanicsEvidenceScope = "controlled-mechanics-proof";
    public const string OptInDiagnosticEvidenceScope = "opt-in-diagnostic";
    public const string NotApplicableEvidenceScope = "not-applicable";

    /// <summary>
    /// Gets whether options match the rollout default provider-overlap evidence contour.
    /// </summary>
    public bool IsDefaultCandidateContour =>
        MatchesDefaultCandidateContour(
            ProviderMode,
            ProviderQueueCapacity,
            ProviderOverlapMode,
            RetentionStrategy,
            ProviderQueueRetainedPayloadBytes,
            OverlapConsumerDelay,
            QueueTelemetryOutput,
            OverlapTelemetryOutput,
            ExecutionMode);

    /// <summary>
    /// Gets whether the run is a controlled producer/consumer overlap proof.
    /// </summary>
    public bool IsControlledProviderOverlapProof =>
        ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
        ProviderOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer &&
        OverlapConsumerDelay > TimeSpan.Zero;

    /// <summary>
    /// Gets the provider-overlap evidence contour label for reporting.
    /// </summary>
    public string ProviderOverlapEvidenceContour =>
        FormatProviderOverlapEvidenceContour(
            ProviderMode,
            ProviderOverlapMode,
            OverlapConsumerDelay,
            IsDefaultCandidateContour);

    /// <summary>
    /// Gets the provider-overlap evidence scope label for reporting.
    /// </summary>
    public string ProviderOverlapEvidenceScope =>
        FormatProviderOverlapEvidenceScope(ProviderOverlapEvidenceContour);

    /// <summary>
    /// Gets explicit or current-default provenance for option values.
    /// </summary>
    public ProcessingBenchmarkArchiveRebalanceOptionProvenance EffectiveOptionProvenance =>
        OptionProvenance ?? ProcessingBenchmarkArchiveRebalanceOptionProvenance.CurrentDefaults;

    /// <summary>
    /// Gets whether the provider mode was explicitly set back to blocking borrowed.
    /// </summary>
    public bool IsExplicitBlockingBorrowedFallback =>
        ProviderMode == RadarProcessingArchiveProviderMode.BlockingBorrowed &&
        EffectiveOptionProvenance.ProviderMode == ProcessingBenchmarkOptionValueSource.Explicit;

    /// <summary>
    /// Gets whether rollout defaults expanded into the default evidence contour.
    /// </summary>
    public bool IsRolloutDefaultExpandedContour =>
        IsDefaultCandidateContour &&
        EffectiveOptionProvenance.ProviderMode == ProcessingBenchmarkOptionValueSource.RolloutDefault;

    /// <summary>
    /// Checks whether supplied provider and execution options match the rollout default evidence contour.
    /// </summary>
    public static bool MatchesDefaultCandidateContour(
        RadarProcessingArchiveProviderMode providerMode,
        int providerQueueCapacity,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? providerQueueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput,
        RadarProcessingExecutionMode executionMode) =>
        providerMode == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode &&
        providerQueueCapacity == DefaultCandidateProviderQueueCapacity &&
        providerOverlapMode == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode &&
        retentionStrategy == RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy &&
        providerQueueRetainedPayloadBytes == DefaultCandidateRetainedPayloadBytes &&
        overlapConsumerDelay == RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay &&
        queueTelemetryOutput != ProcessingBenchmarkProviderQueueTelemetryOutput.None &&
        overlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None &&
        executionMode == RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;

    /// <summary>
    /// Formats the provider-overlap evidence contour from provider and overlap settings.
    /// </summary>
    public static string FormatProviderOverlapEvidenceContour(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        TimeSpan overlapConsumerDelay,
        bool isDefaultCandidateContour)
    {
        if (overlapConsumerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapConsumerDelay));
        }

        if (isDefaultCandidateContour)
        {
            return NaturalDefaultCandidateEvidenceContour;
        }

        return providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? overlapConsumerDelay > TimeSpan.Zero
                ? ControlledProofEvidenceContour
                : NaturalOptInEvidenceContour
            : NotApplicableEvidenceContour;
    }

    /// <summary>
    /// Formats the evidence scope label for a provider-overlap contour.
    /// </summary>
    public static string FormatProviderOverlapEvidenceScope(string providerOverlapEvidenceContour)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOverlapEvidenceContour);

        return providerOverlapEvidenceContour switch
        {
            NaturalDefaultCandidateEvidenceContour => NaturalReadinessEvidenceScope,
            ControlledProofEvidenceContour => ControlledMechanicsEvidenceScope,
            NaturalOptInEvidenceContour => OptInDiagnosticEvidenceScope,
            NotApplicableEvidenceContour => NotApplicableEvidenceScope,
            _ => throw new ArgumentException(
                "Unknown provider overlap evidence contour.",
                nameof(providerOverlapEvidenceContour))
        };
    }

    /// <summary>
    /// Parses archive rebalance benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkArchiveRebalanceOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> modes = Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession
        ]);
        var partitionCount = 24;
        var shardCount = 4;
        var iterations = 1;
        var warmupIterations = 0;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var validationProfile = RadarProcessingValidationProfile.Diagnostic;
        int? quarantineTtlEvaluations = null;
        int? sustainedCoolingSampleCount = null;
        double? materialPressureChangeThreshold = null;
        var retentionMode = RadarProcessingTelemetryRetentionOptions.Default.RetentionMode;
        var maxRetainedDecisions = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedDecisions;
        var maxRetainedTransitions = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedLifecycleTransitions;
        var maxRetainedAcceptedMoves = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedAcceptedMoves;
        var maxRetainedValidationFailures =
            RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedValidationFailures;
        var skewProfile = RadarProcessingPressureSkewOptions.None.Profile;
        var skewFactor = RadarProcessingPressureSkewOptions.None.Factor;
        var skewPeriod = RadarProcessingPressureSkewOptions.None.Period;
        var providerMode = RadarProcessingArchiveProviderMode.BlockingBorrowed;
        var providerModeWasProvided = false;
        var providerOverlapMode = RadarProcessingQueuedProviderOverlapMode.None;
        var providerOverlapModeWasProvided = false;
        var retentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy;
        var retentionStrategyWasProvided = false;
        long? queueRetainedPayloadBytes = null;
        var queueRetainedPayloadBytesWasProvided = false;
        TimeSpan? queueTimeout = null;
        var queueTelemetryOutput = ProcessingBenchmarkProviderQueueTelemetryOutput.Summary;
        var queueTelemetryWasProvided = false;
        var overlapTelemetryOutput = ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary;
        var overlapTelemetryWasProvided = false;
        var overlapConsumerDelay = TimeSpan.Zero;
        var overlapConsumerDelayWasProvided = false;
        var executionMode = RadarProcessingExecutionMode.PartitionedBarrier;
        var executionModeWasProvided = false;
        int? workerCount = null;
        var workerCountWasProvided = false;
        int? queueCapacity = null;
        var queueCapacityWasProvided = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--mode":
                    modes = ParseMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--provider":
                    providerMode = ParseProviderMode(RequireValue(args, ref i, "--provider"));
                    providerModeWasProvided = true;
                    break;
                case "--provider-overlap":
                    providerOverlapMode = ParseProviderOverlapMode(RequireValue(args, ref i, "--provider-overlap"));
                    providerOverlapModeWasProvided = true;
                    break;
                case "--retention-strategy":
                    retentionStrategy = ParseRetentionStrategy(RequireValue(args, ref i, "--retention-strategy"));
                    retentionStrategyWasProvided = true;
                    break;
                case "--execution":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--execution"));
                    executionModeWasProvided = true;
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    workerCountWasProvided = true;
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    queueCapacityWasProvided = true;
                    break;
                case "--queue-timeout-ms":
                    queueTimeout = TimeSpan.FromMilliseconds(
                        double.Parse(RequireValue(args, ref i, "--queue-timeout-ms"), CultureInfo.InvariantCulture));
                    break;
                case "--queue-retained-bytes":
                    queueRetainedPayloadBytes = long.Parse(RequireValue(args, ref i, "--queue-retained-bytes"));
                    queueRetainedPayloadBytesWasProvided = true;
                    break;
                case "--queue-telemetry":
                    queueTelemetryOutput = ParseQueueTelemetryOutput(RequireValue(args, ref i, "--queue-telemetry"));
                    queueTelemetryWasProvided = true;
                    break;
                case "--overlap-telemetry":
                    overlapTelemetryOutput = ParseOverlapTelemetryOutput(
                        RequireValue(args, ref i, "--overlap-telemetry"));
                    overlapTelemetryWasProvided = true;
                    break;
                case "--overlap-consumer-delay-ms":
                    overlapConsumerDelay = TimeSpan.FromMilliseconds(
                        double.Parse(
                            RequireValue(args, ref i, "--overlap-consumer-delay-ms"),
                            CultureInfo.InvariantCulture));
                    overlapConsumerDelayWasProvided = true;
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
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
                case "--retention-mode":
                    retentionMode = ParseRetentionMode(RequireValue(args, ref i, "--retention-mode"));
                    break;
                case "--max-retained-decisions":
                    maxRetainedDecisions = int.Parse(RequireValue(args, ref i, "--max-retained-decisions"));
                    break;
                case "--max-retained-transitions":
                case "--max-retained-lifecycle-transitions":
                    maxRetainedTransitions = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--max-retained-accepted-moves":
                    maxRetainedAcceptedMoves = int.Parse(RequireValue(args, ref i, "--max-retained-accepted-moves"));
                    break;
                case "--max-retained-validation-failures":
                    maxRetainedValidationFailures = int.Parse(
                        RequireValue(args, ref i, "--max-retained-validation-failures"));
                    break;
                case "--skew-profile":
                case "--pressure-skew-profile":
                    skewProfile = ParsePressureSkewProfile(RequireValue(args, ref i, args[i]));
                    break;
                case "--skew-factor":
                case "--pressure-skew-factor":
                    skewFactor = double.Parse(
                        RequireValue(args, ref i, args[i]),
                        CultureInfo.InvariantCulture);
                    break;
                case "--skew-period":
                case "--pressure-skew-period":
                    skewPeriod = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        var providerModeSource = CurrentDefaultOrExplicit(providerModeWasProvided);
        var providerOverlapModeSource = CurrentDefaultOrExplicit(providerOverlapModeWasProvided);
        var retentionStrategySource = CurrentDefaultOrExplicit(retentionStrategyWasProvided);
        var queueCapacitySource = CurrentDefaultOrExplicit(queueCapacityWasProvided);
        var queueRetainedPayloadBytesSource = CurrentDefaultOrExplicit(queueRetainedPayloadBytesWasProvided);
        var queueTelemetrySource = CurrentDefaultOrExplicit(queueTelemetryWasProvided);
        var overlapTelemetrySource = CurrentDefaultOrExplicit(overlapTelemetryWasProvided);
        var overlapConsumerDelaySource = CurrentDefaultOrExplicit(overlapConsumerDelayWasProvided);
        var executionModeSource = CurrentDefaultOrExplicit(executionModeWasProvided);
        var workerCountSource = CurrentDefaultOrExplicit(workerCountWasProvided);

        if (!providerModeWasProvided)
        {
            providerMode = RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode;
            providerModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;

            if (!providerOverlapModeWasProvided)
            {
                providerOverlapMode = RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode;
                providerOverlapModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!retentionStrategyWasProvided)
            {
                retentionStrategy = RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy;
                retentionStrategySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueCapacityWasProvided)
            {
                queueCapacity = DefaultRolloutProviderQueueCapacity;
                queueCapacitySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueRetainedPayloadBytesWasProvided)
            {
                queueRetainedPayloadBytes = DefaultRolloutRetainedPayloadBytes;
                queueRetainedPayloadBytesSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueTelemetryWasProvided)
            {
                queueTelemetrySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!overlapTelemetryWasProvided)
            {
                overlapTelemetrySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!overlapConsumerDelayWasProvided)
            {
                overlapConsumerDelaySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!executionModeWasProvided)
            {
                executionMode = RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;
                executionModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!workerCountWasProvided &&
                executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                workerCount = DefaultRolloutWorkerCount;
                workerCountSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (partitionCount <= 0)
        {
            throw new InvalidOperationException("--partitions must be greater than zero.");
        }

        if (shardCount <= 0)
        {
            throw new InvalidOperationException("--shards must be greater than zero.");
        }

        if (partitionCount < shardCount)
        {
            throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        if (workerCount.HasValue && workerCount.Value <= 0)
        {
            throw new InvalidOperationException("--workers must be greater than zero.");
        }

        if (queueCapacity.HasValue && queueCapacity.Value <= 0)
        {
            throw new InvalidOperationException("--queue-capacity must be greater than zero.");
        }

        if (queueTimeout.HasValue &&
            queueTimeout.Value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("--queue-timeout-ms must be greater than zero.");
        }

        if (queueTimeout.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--queue-timeout-ms requires --provider queued-owned.");
        }

        if (queueRetainedPayloadBytes.HasValue &&
            queueRetainedPayloadBytes.Value <= 0)
        {
            throw new InvalidOperationException("--queue-retained-bytes must be greater than zero.");
        }

        if (queueRetainedPayloadBytes.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--queue-retained-bytes requires --provider queued-owned.");
        }

        if (providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.None &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--provider-overlap requires --provider queued-owned.");
        }

        if (retentionStrategyWasProvided &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--retention-strategy requires --provider queued-owned.");
        }

        if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            retentionStrategy == RadarProcessingRetainedPayloadStrategy.BuilderTransfer)
        {
            throw new InvalidOperationException("--retention-strategy builder-transfer is not supported yet.");
        }

        if (overlapTelemetryWasProvided &&
            overlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.None)
        {
            throw new InvalidOperationException("--overlap-telemetry requires --provider-overlap producer-consumer.");
        }

        if (overlapConsumerDelayWasProvided &&
            overlapConsumerDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("--overlap-consumer-delay-ms must be greater than zero.");
        }

        if (overlapConsumerDelayWasProvided &&
            (providerMode != RadarProcessingArchiveProviderMode.QueuedOwned ||
             providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.ProducerConsumer))
        {
            throw new InvalidOperationException(
                "--overlap-consumer-delay-ms requires --provider queued-owned --provider-overlap producer-consumer.");
        }

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? shardCount,
                queueCapacity: queueCapacity ?? 1);
        }
        else if (workerCount.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --execution async.");
        }
        else if (queueCapacity.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--queue-capacity requires --execution async or --provider queued-owned.");
        }

        var providerQueueCapacity = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned
            ? queueCapacity ?? 1
            : 1;

        ArchiveBZip2Decompressors.Create(decompressor);
        var telemetryRetention = new RadarProcessingTelemetryRetentionOptions(
            retentionMode,
            maxRetainedDecisions,
            maxRetainedTransitions,
            maxRetainedAcceptedMoves,
            maxRetainedValidationFailures);
        var pressureSkew = new RadarProcessingPressureSkewOptions(
            skewProfile,
            skewFactor,
            skewPeriod);
        var quarantineLifecycleOverrides = new ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
            quarantineTtlEvaluations,
            sustainedCoolingSampleCount,
            materialPressureChangeThreshold);
        _ = quarantineLifecycleOverrides.ApplyTo(RadarProcessingQuarantineLifecycleOptions.Default);

        return new ProcessingBenchmarkArchiveRebalanceOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            modes,
            partitionCount,
            shardCount,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            validationProfile,
            quarantineLifecycleOverrides,
            telemetryRetention,
            pressureSkew,
            providerMode,
            providerQueueCapacity,
            queueTimeout,
            providerOverlapMode,
            retentionStrategy,
            queueRetainedPayloadBytes,
            overlapConsumerDelay,
            queueTelemetryOutput,
            overlapTelemetryOutput,
            executionMode,
            asyncExecution,
            new ProcessingBenchmarkArchiveRebalanceOptionProvenance(
                providerModeSource,
                providerOverlapModeSource,
                retentionStrategySource,
                queueCapacitySource,
                queueRetainedPayloadBytesSource,
                queueTelemetrySource,
                overlapTelemetrySource,
                overlapConsumerDelaySource,
                executionModeSource,
                workerCountSource));
    }

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

/// <summary>
/// CLI options for ordered archive processing benchmark runs.
/// </summary>
public sealed record ProcessingBenchmarkOrderedArchiveProcessingOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int PartitionCount,
    int ShardCount,
    int ActiveBatchCapacity,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    ProcessingBenchmarkProviderQueueTelemetryOutput QueueTelemetryOutput =
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
    ProcessingBenchmarkProviderOverlapTelemetryOutput OverlapTelemetryOutput =
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary)
{
    /// <summary>
    /// Parses ordered archive processing benchmark options from CLI arguments.
    /// </summary>
    public static ProcessingBenchmarkOrderedArchiveProcessingOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var partitionCount = 24;
        var shardCount = 4;
        var activeBatchCapacity = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity;
        var iterations = 1;
        var warmupIterations = 0;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var handlerSet = RadarProcessingBenchmarkHandlerSet.None;
        var queueTelemetryOutput = ProcessingBenchmarkProviderQueueTelemetryOutput.Summary;
        var overlapTelemetryOutput = ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--active-batches":
                case "--active-batch-capacity":
                    activeBatchCapacity = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--handlers":
                    handlerSet = ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                case "--queue-telemetry":
                    queueTelemetryOutput = ParseQueueTelemetryOutput(RequireValue(args, ref i, "--queue-telemetry"));
                    break;
                case "--overlap-telemetry":
                    overlapTelemetryOutput = ParseOverlapTelemetryOutput(
                        RequireValue(args, ref i, "--overlap-telemetry"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (partitionCount <= 0)
        {
            throw new InvalidOperationException("--partitions must be greater than zero.");
        }

        if (shardCount <= 0)
        {
            throw new InvalidOperationException("--shards must be greater than zero.");
        }

        if (partitionCount < shardCount)
        {
            throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
        }

        if (activeBatchCapacity <= 0)
        {
            throw new InvalidOperationException("--active-batches must be greater than zero.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
        _ = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity);

        return new ProcessingBenchmarkOrderedArchiveProcessingOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            handlerSet,
            queueTelemetryOutput,
            overlapTelemetryOutput);
    }

    private static RadarProcessingBenchmarkHandlerSet ParseHandlerSet(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => RadarProcessingBenchmarkHandlerSet.None,
            "counter-checksum" => RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            "counter-checksum-heavy" or "counter-checksum+heavy" or "standard-heavy" =>
                RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy,
            _ => throw new ArgumentException($"Unknown ordered archive processing handler set: {value}")
        };

    private static ProcessingBenchmarkProviderQueueTelemetryOutput ParseQueueTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderQueueTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderQueueTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown ordered archive processing queue telemetry mode: {value}")
        };

    private static ProcessingBenchmarkProviderOverlapTelemetryOutput ParseOverlapTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderOverlapTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown ordered archive processing overlap telemetry mode: {value}")
        };

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

internal sealed record ArchiveBenchmarkDecompressionOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive decompression benchmark options from CLI arguments.
    /// </summary>
    public static ArchiveBenchmarkDecompressionOptions Parse(string[] args)
    {
        string? filePath = null;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkDecompressionOptions(filePath, iterations, warmupIterations, parallelism, decompressor);
    }

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

internal sealed record ArchiveBenchmarkParseOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    bool DecodeMomentValues,
    bool DecodeCalibratedMomentValues)
{
    /// <summary>
    /// Parses Archive II parse benchmark options from CLI arguments.
    /// </summary>
    public static ArchiveBenchmarkParseOptions Parse(string[] args)
    {
        string? filePath = null;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var decodeMomentValues = false;
        var decodeCalibratedMomentValues = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--decode-moments":
                    decodeMomentValues = true;
                    break;
                case "--decode-calibrated-moments":
                    decodeMomentValues = true;
                    decodeCalibratedMomentValues = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkParseOptions(
            filePath,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            decodeMomentValues,
            decodeCalibratedMomentValues);
    }

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

internal sealed record ArchiveBenchmarkReplayShapeOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive replay shape benchmark options from CLI arguments.
    /// </summary>
    public static ArchiveBenchmarkReplayShapeOptions Parse(string[] args)
    {
        string? filePath = null;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkReplayShapeOptions(
            filePath,
            iterations,
            warmupIterations,
            parallelism,
            decompressor);
    }

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

internal sealed record ArchiveBenchmarkReplayPublishOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive replay publish benchmark options from CLI arguments.
    /// </summary>
    public static ArchiveBenchmarkReplayPublishOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkReplayPublishOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            iterations,
            warmupIterations,
            parallelism,
            decompressor);
    }

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

internal sealed record ArchiveBenchmarkStreamOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive radar-event-batch stream benchmark options from CLI arguments.
    /// </summary>
    public static ArchiveBenchmarkStreamOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkStreamOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            iterations,
            warmupIterations,
            parallelism,
            decompressor);
    }

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

internal sealed record ArchiveReplayOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive replay publishing options from CLI arguments.
    /// </summary>
    public static ArchiveReplayOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveReplayOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            parallelism,
            decompressor);
    }

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

internal sealed record ArchiveStreamOptions(
    string FilePath,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses Archive II to radar-event-batch stream options from CLI arguments.
    /// </summary>
    public static ArchiveStreamOptions Parse(string[] args)
    {
        string? filePath = null;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveStreamOptions(filePath, parallelism, decompressor);
    }

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

internal sealed record ArchiveValidateDecompressionOptions(
    string? FilePath,
    string? CachePath,
    string? RadarId,
    int MaxFiles)
{
    /// <summary>
    /// Parses archive decompression validation options from CLI arguments.
    /// </summary>
    public static ArchiveValidateDecompressionOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        string? radarId = null;
        var maxFiles = 20;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) && radarId is not null)
        {
            throw new InvalidOperationException("--radar can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        return new ArchiveValidateDecompressionOptions(filePath, cachePath, radarId, maxFiles);
    }

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

internal sealed record ArchiveValidateReplayShapeOptions(
    string? FilePath,
    string? CachePath,
    string? RadarId,
    int MaxFiles,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive replay shape validation options from CLI arguments.
    /// </summary>
    public static ArchiveValidateReplayShapeOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        string? radarId = null;
        var maxFiles = int.MaxValue;
        var parallelism = Math.Max(1, Environment.ProcessorCount);
        var decompressor = ArchiveBZip2Decompressors.DefaultName;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) && radarId is not null)
        {
            throw new InvalidOperationException("--radar can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveValidateReplayShapeOptions(
            filePath,
            cachePath,
            radarId,
            maxFiles,
            parallelism,
            decompressor);
    }

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

internal sealed record ProductPipelineDemoOptions(
    string RunId,
    int SourceCount,
    int BatchCount,
    int EventsPerBatch,
    int PartitionCount,
    int ShardCount,
    RadarPulseProductHandlerSet HandlerSet,
    RadarPulseProductPipelineOptions PipelineOptions)
{
    /// <summary>
    /// Parses product pipeline synthetic demo options from CLI arguments.
    /// </summary>
    public static ProductPipelineDemoOptions Parse(string[] args)
    {
        var runId = "product-demo";
        var sourceCount = 2;
        var batchCount = 2;
        var eventsPerBatch = 2;
        var partitionCount = 0;
        var shardCount = 0;
        var handlerSet = RadarPulseProductHandlerSet.None;
        int? workerCount = null;
        int? workerQueueCapacity = null;
        int? providerQueueCapacity = null;
        int? orderedActiveBatchCapacity = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--run-id":
                    runId = RequireValue(args, ref i, "--run-id");
                    break;
                case "--sources":
                    sourceCount = int.Parse(RequireValue(args, ref i, "--sources"));
                    break;
                case "--batches":
                    batchCount = int.Parse(RequireValue(args, ref i, "--batches"));
                    break;
                case "--events-per-batch":
                    eventsPerBatch = int.Parse(RequireValue(args, ref i, "--events-per-batch"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--handlers":
                    handlerSet = ProductPipelineCliOptionParsing.ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--worker-queue-capacity":
                    workerQueueCapacity = int.Parse(RequireValue(args, ref i, "--worker-queue-capacity"));
                    break;
                case "--provider-queue-capacity":
                    providerQueueCapacity = int.Parse(RequireValue(args, ref i, "--provider-queue-capacity"));
                    break;
                case "--active-batches":
                    orderedActiveBatchCapacity = int.Parse(RequireValue(args, ref i, "--active-batches"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        ProductPipelineCliOptionParsing.ValidatePositive(sourceCount, "--sources");
        ProductPipelineCliOptionParsing.ValidatePositive(batchCount, "--batches");
        ProductPipelineCliOptionParsing.ValidatePositive(eventsPerBatch, "--events-per-batch");
        ProductPipelineCliOptionParsing.ValidateNonNegative(partitionCount, "--partitions");
        ProductPipelineCliOptionParsing.ValidateNonNegative(shardCount, "--shards");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(workerCount, "--workers");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(workerQueueCapacity, "--worker-queue-capacity");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(providerQueueCapacity, "--provider-queue-capacity");
        ProductPipelineCliOptionParsing.ValidateOptionalPositive(orderedActiveBatchCapacity, "--active-batches");

        return new ProductPipelineDemoOptions(
            runId,
            sourceCount,
            batchCount,
            eventsPerBatch,
            partitionCount,
            shardCount,
            handlerSet,
            new RadarPulseProductPipelineOptions(
                WorkerCount: workerCount,
                WorkerQueueCapacity: workerQueueCapacity,
                ProviderQueueCapacity: providerQueueCapacity,
                OrderedActiveBatchCapacity: orderedActiveBatchCapacity));
    }

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

internal sealed record ProductPipelineArchiveOptions(
    string RunId,
    string FilePath,
    int Parallelism,
    int PartitionCount,
    int ShardCount,
    string Decompressor,
    RadarPulseProductHandlerSet HandlerSet,
    RadarPulseProductPipelineOptions PipelineOptions)
{
    /// <summary>
    /// Parses product pipeline archive run options from CLI arguments.
    /// </summary>
    public static ProductPipelineArchiveOptions Parse(string[] args)
    {
        var runId = "product-archive";
        string? filePath = null;
        var parallelism = 1;
        var partitionCount = 0;
        var shardCount = 0;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var handlerSet = RadarPulseProductHandlerSet.None;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--run-id":
                    runId = RequireValue(args, ref i, "--run-id");
                    break;
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--handlers":
                    handlerSet = ProductPipelineCliOptionParsing.ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        ProductPipelineCliOptionParsing.ValidatePositive(parallelism, "--parallelism");
        ProductPipelineCliOptionParsing.ValidateNonNegative(partitionCount, "--partitions");
        ProductPipelineCliOptionParsing.ValidateNonNegative(shardCount, "--shards");
        ArchiveBZip2Decompressors.Create(decompressor);

        return new ProductPipelineArchiveOptions(
            runId,
            filePath,
            parallelism,
            partitionCount,
            shardCount,
            decompressor,
            handlerSet,
            new RadarPulseProductPipelineOptions());
    }

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

internal static class ProductPipelineCliOptionParsing
{
    /// <summary>
    /// Parses the product pipeline handler set option.
    /// </summary>
    public static RadarPulseProductHandlerSet ParseHandlerSet(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "none" => RadarPulseProductHandlerSet.None,
            "counter-checksum" => RadarPulseProductHandlerSet.CounterChecksum,
            "counter-checksum-heavy" => RadarPulseProductHandlerSet.CounterChecksumHeavy,
            "snapshot-counting" => RadarPulseProductHandlerSet.SnapshotCounting,
            "unsupported" => RadarPulseProductHandlerSet.Unsupported,
            _ => throw new ArgumentException($"Unknown product handler set: {value}.")
        };

    /// <summary>
    /// Validates that an integer option is positive.
    /// </summary>
    public static void ValidatePositive(int value, string option)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{option} must be greater than zero.");
        }
    }

    /// <summary>
    /// Validates that an integer option is non-negative.
    /// </summary>
    public static void ValidateNonNegative(int value, string option)
    {
        if (value < 0)
        {
            throw new InvalidOperationException($"{option} cannot be negative.");
        }
    }

    /// <summary>
    /// Validates that an optional integer option is positive when supplied.
    /// </summary>
    public static void ValidateOptionalPositive(int? value, string option)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{option} must be greater than zero.");
        }
    }
}

/// <summary>
/// Selects how provider queue telemetry is written by processing benchmark CLI commands.
/// </summary>
public enum ProcessingBenchmarkProviderQueueTelemetryOutput
{
    /// <summary>
    /// Suppresses provider queue telemetry output.
    /// </summary>
    None = 1,

    /// <summary>
    /// Writes aggregate provider queue telemetry.
    /// </summary>
    Summary = 2,

    /// <summary>
    /// Writes aggregate and recent-detail provider queue telemetry.
    /// </summary>
    Recent = 3
}

/// <summary>
/// Selects how provider overlap telemetry is written by processing benchmark CLI commands.
/// </summary>
public enum ProcessingBenchmarkProviderOverlapTelemetryOutput
{
    /// <summary>
    /// Suppresses provider overlap telemetry output.
    /// </summary>
    None = 1,

    /// <summary>
    /// Writes aggregate provider overlap telemetry.
    /// </summary>
    Summary = 2,

    /// <summary>
    /// Writes aggregate and recent-detail provider overlap telemetry.
    /// </summary>
    Recent = 3
}

/// <summary>
/// Identifies where a processing benchmark option value came from.
/// </summary>
public enum ProcessingBenchmarkOptionValueSource
{
    /// <summary>
    /// The option used the current command default.
    /// </summary>
    CurrentDefault = 0,

    /// <summary>
    /// The option was provided explicitly by the operator.
    /// </summary>
    Explicit = 1,

    /// <summary>
    /// The option was expanded from rollout defaults.
    /// </summary>
    RolloutDefault = 2
}

/// <summary>
/// Captures provenance for archive rebalance benchmark options that affect rollout evidence contours.
/// </summary>
public sealed record ProcessingBenchmarkArchiveRebalanceOptionProvenance(
    ProcessingBenchmarkOptionValueSource ProviderMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource ProviderOverlapMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource RetentionStrategy = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueCapacity = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueRetainedPayloadBytes = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueTelemetry = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource OverlapTelemetry = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource OverlapConsumerDelay = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource ExecutionMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource WorkerCount = ProcessingBenchmarkOptionValueSource.CurrentDefault)
{
    /// <summary>
    /// Gets provenance where every option used the current command default.
    /// </summary>
    public static ProcessingBenchmarkArchiveRebalanceOptionProvenance CurrentDefaults { get; } = new();
}

