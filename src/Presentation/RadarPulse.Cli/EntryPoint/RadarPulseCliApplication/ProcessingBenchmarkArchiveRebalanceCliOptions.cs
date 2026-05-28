using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

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
