using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingProductionPipelineOptions
{
    public RadarProcessingProductionPipelineOptions(
        string? profileName = null,
        RadarProcessingArchiveProviderMode? providerMode = null,
        RadarProcessingQueuedProviderOverlapMode? providerOverlapMode = null,
        RadarProcessingRetainedPayloadStrategy? retentionStrategy = null,
        RadarProcessingExecutionMode? executionMode = null,
        int? workerCount = null,
        int? workerQueueCapacity = null,
        int? providerQueueCapacity = null,
        long? retainedPayloadBytes = null,
        int? orderedActiveBatchCapacity = null,
        RadarProcessingProductionPipelineDurableAdapterKind? durableAdapterKind = null,
        RadarProcessingProductionPipelineHandlerMode? handlerMode = null,
        int? workloadBatchLimit = null,
        bool silentBorrowedProviderFallback = false,
        RadarProcessingProductionPipelineOptionSource overrideSource =
            RadarProcessingProductionPipelineOptionSource.ExplicitOverride)
    {
        ProfileName = string.IsNullOrWhiteSpace(profileName)
            ? null
            : profileName.Trim();
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
        SilentBorrowedProviderFallback = silentBorrowedProviderFallback;
        OverrideSource = overrideSource;
    }

    public string? ProfileName { get; }

    public RadarProcessingArchiveProviderMode? ProviderMode { get; }

    public RadarProcessingQueuedProviderOverlapMode? ProviderOverlapMode { get; }

    public RadarProcessingRetainedPayloadStrategy? RetentionStrategy { get; }

    public RadarProcessingExecutionMode? ExecutionMode { get; }

    public int? WorkerCount { get; }

    public int? WorkerQueueCapacity { get; }

    public int? ProviderQueueCapacity { get; }

    public long? RetainedPayloadBytes { get; }

    public int? OrderedActiveBatchCapacity { get; }

    public RadarProcessingProductionPipelineDurableAdapterKind? DurableAdapterKind { get; }

    public RadarProcessingProductionPipelineHandlerMode? HandlerMode { get; }

    public int? WorkloadBatchLimit { get; }

    public bool SilentBorrowedProviderFallback { get; }

    public RadarProcessingProductionPipelineOptionSource OverrideSource { get; }
}
