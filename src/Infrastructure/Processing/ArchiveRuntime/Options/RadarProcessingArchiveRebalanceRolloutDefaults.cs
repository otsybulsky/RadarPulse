using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Accepted rollout defaults for archive rebalance and production-pipeline runtime contours.
/// </summary>
public static class RadarProcessingArchiveRebalanceRolloutDefaults
{
    /// <summary>
    /// Accepted archive provider mode.
    /// </summary>
    public const RadarProcessingArchiveProviderMode ProviderMode = RadarProcessingArchiveProviderMode.QueuedOwned;
    /// <summary>
    /// Accepted queued provider overlap mode.
    /// </summary>
    public const RadarProcessingQueuedProviderOverlapMode ProviderOverlapMode =
        RadarProcessingQueuedProviderOverlapMode.ProducerConsumer;
    /// <summary>
    /// Accepted retained payload strategy.
    /// </summary>
    public const RadarProcessingRetainedPayloadStrategy RetentionStrategy =
        RadarProcessingRetainedPayloadStrategy.PooledCopy;
    /// <summary>
    /// Accepted processing execution mode.
    /// </summary>
    public const RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.AsyncShardTransport;
    /// <summary>
    /// Accepted async worker count.
    /// </summary>
    public const int WorkerCount = 4;
    /// <summary>
    /// Accepted per-worker queue capacity.
    /// </summary>
    public const int WorkerQueueCapacity = 8;
    /// <summary>
    /// Accepted provider queue capacity.
    /// </summary>
    public const int ProviderQueueCapacity = 8;
    /// <summary>
    /// Accepted retained payload byte budget.
    /// </summary>
    public const long RetainedPayloadBytes = 536_870_912;
    /// <summary>
    /// Indicates whether retained payload prewarm is part of the accepted contour.
    /// </summary>
    public const bool RetainedPayloadPrewarmEnabled = true;
    /// <summary>
    /// Accepted retained payload prewarm event count.
    /// </summary>
    public const int RetainedPayloadPrewarmEventCount = 65_536;
    /// <summary>
    /// Accepted retained payload prewarm payload bytes.
    /// </summary>
    public const int RetainedPayloadPrewarmPayloadBytes = 64 * 1024 * 1024;
    /// <summary>
    /// Accepted retained payload prewarm batch count.
    /// </summary>
    public const int RetainedPayloadPrewarmBatchCount = 1;

    /// <summary>
    /// Accepted artificial consumer delay for producer/consumer overlap.
    /// </summary>
    public static TimeSpan OverlapConsumerDelay => TimeSpan.Zero;

    /// <summary>
    /// Creates async execution options matching the rollout defaults.
    /// </summary>
    public static RadarProcessingAsyncExecutionOptions CreateAsyncExecution() =>
        new(workerCount: WorkerCount, queueCapacity: WorkerQueueCapacity);

    /// <summary>
    /// Checks whether supplied runtime options match the accepted rollout contour.
    /// </summary>
    public static bool Matches(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        int providerQueueCapacity,
        long? retainedPayloadBytes,
        TimeSpan overlapConsumerDelay) =>
        providerMode == ProviderMode &&
        providerOverlapMode == ProviderOverlapMode &&
        retentionStrategy == RetentionStrategy &&
        executionMode == ExecutionMode &&
        asyncExecution is not null &&
        asyncExecution.WorkerCount == WorkerCount &&
        asyncExecution.QueueCapacity == WorkerQueueCapacity &&
        providerQueueCapacity == ProviderQueueCapacity &&
        retainedPayloadBytes == RetainedPayloadBytes &&
        overlapConsumerDelay == OverlapConsumerDelay;
}
