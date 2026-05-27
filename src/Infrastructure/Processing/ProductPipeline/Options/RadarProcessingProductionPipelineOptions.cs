using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Optional overrides for the accepted production-pipeline profile.
/// </summary>
/// <remarks>
/// The profile resolver validates that overrides remain within the portfolio
/// production-shaped boundary: queued-owned provider, producer-consumer overlap,
/// pooled retention, async shard transport, and file durable persistence.
/// </remarks>
public sealed record RadarProcessingProductionPipelineOptions
{
    /// <summary>
    /// Creates pipeline override options.
    /// </summary>
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

    /// <summary>
    /// Optional profile name reported in product evidence.
    /// </summary>
    public string? ProfileName { get; }

    /// <summary>
    /// Provider mode override.
    /// </summary>
    public RadarProcessingArchiveProviderMode? ProviderMode { get; }

    /// <summary>
    /// Queued provider overlap mode override.
    /// </summary>
    public RadarProcessingQueuedProviderOverlapMode? ProviderOverlapMode { get; }

    /// <summary>
    /// Retained payload strategy override.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy? RetentionStrategy { get; }

    /// <summary>
    /// Processing execution mode override.
    /// </summary>
    public RadarProcessingExecutionMode? ExecutionMode { get; }

    /// <summary>
    /// Async worker count override.
    /// </summary>
    public int? WorkerCount { get; }

    /// <summary>
    /// Per-worker mailbox capacity override.
    /// </summary>
    public int? WorkerQueueCapacity { get; }

    /// <summary>
    /// Provider queue capacity override.
    /// </summary>
    public int? ProviderQueueCapacity { get; }

    /// <summary>
    /// Retained payload byte budget override.
    /// </summary>
    public long? RetainedPayloadBytes { get; }

    /// <summary>
    /// Ordered concurrent active batch capacity override.
    /// </summary>
    public int? OrderedActiveBatchCapacity { get; }

    /// <summary>
    /// Durable adapter kind override.
    /// </summary>
    public RadarProcessingProductionPipelineDurableAdapterKind? DurableAdapterKind { get; }

    /// <summary>
    /// Handler output mode override.
    /// </summary>
    public RadarProcessingProductionPipelineHandlerMode? HandlerMode { get; }

    /// <summary>
    /// Optional workload batch limit for callers that cap input before running.
    /// </summary>
    public int? WorkloadBatchLimit { get; }

    /// <summary>
    /// Indicates an intentionally unsafe fallback request that the profile must reject.
    /// </summary>
    public bool SilentBorrowedProviderFallback { get; }

    /// <summary>
    /// Source label attached to supplied overrides.
    /// </summary>
    public RadarProcessingProductionPipelineOptionSource OverrideSource { get; }
}
