using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Effective production-pipeline configuration after applying profile defaults and overrides.
/// </summary>
/// <remarks>
/// The object preserves both values and sources so product evidence can show
/// which options came from the accepted profile and which came from caller
/// overrides or test harnesses.
/// </remarks>
public sealed class RadarProcessingProductionPipelineResolvedConfiguration
{
    private readonly IReadOnlyList<string> warnings;

    /// <summary>
    /// Creates a resolved configuration with validation and warning evidence.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedConfiguration(
        string profileName,
        RadarProcessingProductionPipelineResolvedOption<RadarProcessingArchiveProviderMode> providerMode,
        RadarProcessingProductionPipelineResolvedOption<RadarProcessingQueuedProviderOverlapMode> providerOverlapMode,
        RadarProcessingProductionPipelineResolvedOption<RadarProcessingRetainedPayloadStrategy> retentionStrategy,
        RadarProcessingProductionPipelineResolvedOption<RadarProcessingExecutionMode> executionMode,
        RadarProcessingProductionPipelineResolvedOption<int> workerCount,
        RadarProcessingProductionPipelineResolvedOption<int> workerQueueCapacity,
        RadarProcessingProductionPipelineResolvedOption<int> providerQueueCapacity,
        RadarProcessingProductionPipelineResolvedOption<long> retainedPayloadBytes,
        RadarProcessingProductionPipelineResolvedOption<int> orderedActiveBatchCapacity,
        RadarProcessingProductionPipelineResolvedOption<RadarProcessingProductionPipelineDurableAdapterKind>
            durableAdapterKind,
        RadarProcessingProductionPipelineResolvedOption<RadarProcessingProductionPipelineHandlerMode> handlerMode,
        RadarProcessingProductionPipelineResolvedOption<int?> workloadBatchLimit,
        bool isValid,
        string? firstInvalidOption = null,
        string? firstInvalidReason = null,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentNullException.ThrowIfNull(providerMode);
        ArgumentNullException.ThrowIfNull(providerOverlapMode);
        ArgumentNullException.ThrowIfNull(retentionStrategy);
        ArgumentNullException.ThrowIfNull(executionMode);
        ArgumentNullException.ThrowIfNull(workerCount);
        ArgumentNullException.ThrowIfNull(workerQueueCapacity);
        ArgumentNullException.ThrowIfNull(providerQueueCapacity);
        ArgumentNullException.ThrowIfNull(retainedPayloadBytes);
        ArgumentNullException.ThrowIfNull(orderedActiveBatchCapacity);
        ArgumentNullException.ThrowIfNull(durableAdapterKind);
        ArgumentNullException.ThrowIfNull(handlerMode);
        ArgumentNullException.ThrowIfNull(workloadBatchLimit);

        ProfileName = profileName;
        ProviderMode = providerMode;
        ProviderOverlapMode = providerOverlapMode;
        RetentionStrategy = retentionStrategy;
        ExecutionMode = executionMode;
        WorkerCount = workerCount;
        WorkerQueueCapacity = workerQueueCapacity;
        ProviderQueueCapacity = providerQueueCapacity;
        RetainedPayloadBytes = retainedPayloadBytes;
        OrderedActiveBatchCapacity = orderedActiveBatchCapacity;
        DurableAdapterKind = durableAdapterKind;
        HandlerMode = handlerMode;
        WorkloadBatchLimit = workloadBatchLimit;
        IsValid = isValid;
        FirstInvalidOption = string.IsNullOrWhiteSpace(firstInvalidOption)
            ? null
            : firstInvalidOption;
        FirstInvalidReason = string.IsNullOrWhiteSpace(firstInvalidReason)
            ? null
            : firstInvalidReason;
        this.warnings = CopyWarnings(warnings);
    }

    /// <summary>
    /// Effective profile name.
    /// </summary>
    public string ProfileName { get; }

    /// <summary>
    /// Effective provider mode.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingArchiveProviderMode> ProviderMode { get; }

    /// <summary>
    /// Effective queued provider overlap mode.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingQueuedProviderOverlapMode>
        ProviderOverlapMode { get; }

    /// <summary>
    /// Effective retained payload strategy.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingRetainedPayloadStrategy>
        RetentionStrategy { get; }

    /// <summary>
    /// Effective processing execution mode.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingExecutionMode> ExecutionMode { get; }

    /// <summary>
    /// Effective async worker count.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<int> WorkerCount { get; }

    /// <summary>
    /// Effective per-worker queue capacity.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<int> WorkerQueueCapacity { get; }

    /// <summary>
    /// Effective provider queue capacity.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<int> ProviderQueueCapacity { get; }

    /// <summary>
    /// Effective retained payload byte budget.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<long> RetainedPayloadBytes { get; }

    /// <summary>
    /// Effective ordered concurrent active batch capacity.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<int> OrderedActiveBatchCapacity { get; }

    /// <summary>
    /// Effective durable adapter kind.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingProductionPipelineDurableAdapterKind>
        DurableAdapterKind { get; }

    /// <summary>
    /// Effective handler output mode.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingProductionPipelineHandlerMode> HandlerMode
    {
        get;
    }

    /// <summary>
    /// Effective optional workload batch limit.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedOption<int?> WorkloadBatchLimit { get; }

    /// <summary>
    /// Indicates whether the configuration satisfies the accepted profile.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// First invalid option name when validation failed.
    /// </summary>
    public string? FirstInvalidOption { get; }

    /// <summary>
    /// First invalid option reason when validation failed.
    /// </summary>
    public string? FirstInvalidReason { get; }

    /// <summary>
    /// Indicates whether profile resolution produced warnings.
    /// </summary>
    public bool HasWarnings => warnings.Count > 0;

    /// <summary>
    /// Warning messages for accepted but notable overrides.
    /// </summary>
    public IReadOnlyList<string> Warnings => warnings;

    /// <summary>
    /// Creates async execution options from resolved worker settings.
    /// </summary>
    public RadarProcessingAsyncExecutionOptions CreateAsyncExecution() =>
        new(WorkerCount.Value, WorkerQueueCapacity.Value);

    /// <summary>
    /// Creates queued-overlap runtime options from resolved queue and retention settings.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapOptions CreateQueuedOverlapOptions() =>
        new(
            new RadarProcessingProviderQueueOptions(
                capacity: ProviderQueueCapacity.Value,
                maxRetainedPayloadBytes: RetainedPayloadBytes.Value),
            new RadarProcessingRetainedPayloadOptions(
                RetentionStrategy.Value,
                RetainedPayloadBytes.Value),
            retainedPayloadPrewarmOptions: RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault);

    /// <summary>
    /// Creates ordered concurrency options from the resolved active batch capacity.
    /// </summary>
    public RadarProcessingOrderedConcurrencyOptions CreateOrderedConcurrencyOptions() =>
        new(OrderedActiveBatchCapacity.Value);

    private static IReadOnlyList<string> CopyWarnings(IReadOnlyList<string>? warnings)
    {
        if (warnings is null || warnings.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[warnings.Count];
        for (var i = 0; i < warnings.Count; i++)
        {
            copy[i] = warnings[i] ?? string.Empty;
        }

        return copy;
    }
}
