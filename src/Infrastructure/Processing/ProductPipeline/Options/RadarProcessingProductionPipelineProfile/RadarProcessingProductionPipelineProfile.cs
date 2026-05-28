using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Resolves and validates the accepted production-pipeline configuration profile.
/// </summary>
public static partial class RadarProcessingProductionPipelineProfile
{
    /// <summary>
    /// Default profile name reported by production-pipeline evidence.
    /// </summary>
    public const string DefaultProfileName = "production-pipeline";

    /// <summary>
    /// Resolves caller options against accepted rollout defaults and validation rules.
    /// </summary>
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
}
