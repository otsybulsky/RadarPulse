using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineResolvedConfiguration
{
    private readonly IReadOnlyList<string> warnings;

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

    public string ProfileName { get; }

    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingArchiveProviderMode> ProviderMode { get; }

    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingQueuedProviderOverlapMode>
        ProviderOverlapMode { get; }

    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingRetainedPayloadStrategy>
        RetentionStrategy { get; }

    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingExecutionMode> ExecutionMode { get; }

    public RadarProcessingProductionPipelineResolvedOption<int> WorkerCount { get; }

    public RadarProcessingProductionPipelineResolvedOption<int> WorkerQueueCapacity { get; }

    public RadarProcessingProductionPipelineResolvedOption<int> ProviderQueueCapacity { get; }

    public RadarProcessingProductionPipelineResolvedOption<long> RetainedPayloadBytes { get; }

    public RadarProcessingProductionPipelineResolvedOption<int> OrderedActiveBatchCapacity { get; }

    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingProductionPipelineDurableAdapterKind>
        DurableAdapterKind { get; }

    public RadarProcessingProductionPipelineResolvedOption<RadarProcessingProductionPipelineHandlerMode> HandlerMode
    {
        get;
    }

    public RadarProcessingProductionPipelineResolvedOption<int?> WorkloadBatchLimit { get; }

    public bool IsValid { get; }

    public string? FirstInvalidOption { get; }

    public string? FirstInvalidReason { get; }

    public bool HasWarnings => warnings.Count > 0;

    public IReadOnlyList<string> Warnings => warnings;

    public RadarProcessingAsyncExecutionOptions CreateAsyncExecution() =>
        new(WorkerCount.Value, WorkerQueueCapacity.Value);

    public RadarProcessingArchiveQueuedOverlapOptions CreateQueuedOverlapOptions() =>
        new(
            new RadarProcessingProviderQueueOptions(
                capacity: ProviderQueueCapacity.Value,
                maxRetainedPayloadBytes: RetainedPayloadBytes.Value),
            new RadarProcessingRetainedPayloadOptions(
                RetentionStrategy.Value,
                RetainedPayloadBytes.Value),
            retainedPayloadPrewarmOptions: RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault);

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
