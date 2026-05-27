using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static class RadarProcessingProductionPipelineProfile
{
    public const string DefaultProfileName = "production-pipeline";

    public static RadarProcessingProductionPipelineResolvedConfiguration Resolve(
        RadarProcessingProductionPipelineOptions? options = null)
    {
        options ??= new RadarProcessingProductionPipelineOptions();

        var source = NormalizeOverrideSource(options.OverrideSource);
        var invalid = new FirstInvalid();
        var warnings = new List<string>();

        var profileName = options.ProfileName ?? DefaultProfileName;
        var providerMode = Resolve(
            options.ProviderMode,
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode,
            source);
        var providerOverlapMode = Resolve(
            options.ProviderOverlapMode,
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode,
            source);
        var retentionStrategy = Resolve(
            options.RetentionStrategy,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
            source);
        var executionMode = Resolve(
            options.ExecutionMode,
            RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode,
            source);
        var workerCount = Resolve(
            options.WorkerCount,
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount,
            source);
        var workerQueueCapacity = Resolve(
            options.WorkerQueueCapacity,
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            source);
        var providerQueueCapacity = Resolve(
            options.ProviderQueueCapacity,
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
            source);
        var retainedPayloadBytes = Resolve(
            options.RetainedPayloadBytes,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            source);
        var orderedActiveBatchCapacity = Resolve(
            options.OrderedActiveBatchCapacity,
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            source);
        var durableAdapterKind = Resolve(
            options.DurableAdapterKind,
            RadarProcessingProductionPipelineDurableAdapterKind.File,
            source);
        var handlerMode = Resolve(
            options.HandlerMode,
            RadarProcessingProductionPipelineHandlerMode.Auto,
            source);
        var workloadBatchLimit = ResolveNullable(
            options.WorkloadBatchLimit,
            source);

        ValidateProviderMode(providerMode.Value, invalid);
        ValidateProviderOverlapMode(providerOverlapMode.Value, invalid);
        ValidateRetentionStrategy(retentionStrategy.Value, invalid);
        ValidateExecutionMode(executionMode.Value, invalid);
        ValidatePositive(workerCount.Value, nameof(options.WorkerCount), invalid);
        ValidatePositive(workerQueueCapacity.Value, nameof(options.WorkerQueueCapacity), invalid);
        ValidatePositive(providerQueueCapacity.Value, nameof(options.ProviderQueueCapacity), invalid);
        ValidatePositive(retainedPayloadBytes.Value, nameof(options.RetainedPayloadBytes), invalid);
        ValidatePositive(orderedActiveBatchCapacity.Value, nameof(options.OrderedActiveBatchCapacity), invalid);
        ValidateDurableAdapterKind(durableAdapterKind.Value, invalid);
        ValidateHandlerMode(handlerMode.Value, invalid);
        ValidateNullablePositive(workloadBatchLimit.Value, nameof(options.WorkloadBatchLimit), invalid);

        if (options.SilentBorrowedProviderFallback)
        {
            invalid.Set(
                nameof(options.SilentBorrowedProviderFallback),
                "Silent borrowed-provider fallback is not accepted by the production pipeline profile.");
        }

        AddOverrideWarning(providerMode, RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode, warnings);
        AddOverrideWarning(providerOverlapMode, RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode, warnings);
        AddOverrideWarning(retentionStrategy, RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy, warnings);
        AddOverrideWarning(executionMode, RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode, warnings);
        AddOverrideWarning(workerCount, RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, warnings);
        AddOverrideWarning(workerQueueCapacity, RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity, warnings);
        AddOverrideWarning(providerQueueCapacity, RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, warnings);
        AddOverrideWarning(retainedPayloadBytes, RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes, warnings);
        AddOverrideWarning(
            orderedActiveBatchCapacity,
            RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity,
            warnings);

        return new RadarProcessingProductionPipelineResolvedConfiguration(
            profileName,
            providerMode,
            providerOverlapMode,
            retentionStrategy,
            executionMode,
            workerCount,
            workerQueueCapacity,
            providerQueueCapacity,
            retainedPayloadBytes,
            orderedActiveBatchCapacity,
            durableAdapterKind,
            handlerMode,
            workloadBatchLimit,
            !invalid.HasValue,
            invalid.Option,
            invalid.Reason,
            warnings);
    }

    private static RadarProcessingProductionPipelineResolvedOption<T> Resolve<T>(
        T? value,
        T defaultValue,
        RadarProcessingProductionPipelineOptionSource source)
        where T : struct =>
        value.HasValue
            ? new RadarProcessingProductionPipelineResolvedOption<T>(value.Value, source)
            : new RadarProcessingProductionPipelineResolvedOption<T>(
                defaultValue,
                RadarProcessingProductionPipelineOptionSource.Profile);

    private static RadarProcessingProductionPipelineResolvedOption<int?> ResolveNullable(
        int? value,
        RadarProcessingProductionPipelineOptionSource source) =>
        value.HasValue
            ? new RadarProcessingProductionPipelineResolvedOption<int?>(value.Value, source)
            : new RadarProcessingProductionPipelineResolvedOption<int?>(
                null,
                RadarProcessingProductionPipelineOptionSource.Default);

    private static RadarProcessingProductionPipelineOptionSource NormalizeOverrideSource(
        RadarProcessingProductionPipelineOptionSource source) =>
        source is RadarProcessingProductionPipelineOptionSource.ExplicitOverride or
            RadarProcessingProductionPipelineOptionSource.TestHarness
            ? source
            : RadarProcessingProductionPipelineOptionSource.ExplicitOverride;

    private static void ValidateProviderMode(
        RadarProcessingArchiveProviderMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.ProviderMode),
                "Production pipeline provider mode must remain queued-owned.");
        }
    }

    private static void ValidateProviderOverlapMode(
        RadarProcessingQueuedProviderOverlapMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.ProviderOverlapMode),
                "Production pipeline provider overlap must remain producer-consumer.");
        }
    }

    private static void ValidateRetentionStrategy(
        RadarProcessingRetainedPayloadStrategy value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingRetainedPayloadStrategy.PooledCopy)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.RetentionStrategy),
                "Production pipeline retention strategy must remain pooled-copy.");
        }
    }

    private static void ValidateExecutionMode(
        RadarProcessingExecutionMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.ExecutionMode),
                "Production pipeline execution mode must remain async shard transport.");
        }
    }

    private static void ValidateDurableAdapterKind(
        RadarProcessingProductionPipelineDurableAdapterKind value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingProductionPipelineDurableAdapterKind.File)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.DurableAdapterKind),
                "Milestone 027 accepts only the file durable adapter kind.");
        }
    }

    private static void ValidateHandlerMode(
        RadarProcessingProductionPipelineHandlerMode value,
        FirstInvalid invalid)
    {
        if (value is not RadarProcessingProductionPipelineHandlerMode.Auto and
            not RadarProcessingProductionPipelineHandlerMode.HandlerFree and
            not RadarProcessingProductionPipelineHandlerMode.MergeableDelta and
            not RadarProcessingProductionPipelineHandlerMode.SnapshotSequential)
        {
            invalid.Set(
                nameof(RadarProcessingProductionPipelineOptions.HandlerMode),
                "Unknown production pipeline handler mode.");
        }
    }

    private static void ValidatePositive<T>(
        T value,
        string option,
        FirstInvalid invalid)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) <= 0)
        {
            invalid.Set(option, "Production pipeline numeric options must be positive.");
        }
    }

    private static void ValidateNullablePositive(
        int? value,
        string option,
        FirstInvalid invalid)
    {
        if (value.HasValue && value.Value <= 0)
        {
            invalid.Set(option, "Production pipeline workload limits must be positive when specified.");
        }
    }

    private static void AddOverrideWarning<T>(
        RadarProcessingProductionPipelineResolvedOption<T> option,
        T acceptedDefault,
        List<string> warnings)
    {
        if (option.Source == RadarProcessingProductionPipelineOptionSource.Profile ||
            option.Source == RadarProcessingProductionPipelineOptionSource.Default ||
            EqualityComparer<T>.Default.Equals(option.Value, acceptedDefault))
        {
            return;
        }

        warnings.Add(
            $"Explicit production pipeline override changes accepted default to {option.Value}.");
    }

    private sealed class FirstInvalid
    {
        public string? Option { get; private set; }

        public string? Reason { get; private set; }

        public bool HasValue => Option is not null;

        public void Set(string option, string reason)
        {
            if (HasValue)
            {
                return;
            }

            Option = option;
            Reason = reason;
        }
    }
}
