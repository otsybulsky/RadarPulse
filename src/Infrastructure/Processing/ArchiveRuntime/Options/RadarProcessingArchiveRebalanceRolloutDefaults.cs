using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static class RadarProcessingArchiveRebalanceRolloutDefaults
{
    public const RadarProcessingArchiveProviderMode ProviderMode = RadarProcessingArchiveProviderMode.QueuedOwned;
    public const RadarProcessingQueuedProviderOverlapMode ProviderOverlapMode =
        RadarProcessingQueuedProviderOverlapMode.ProducerConsumer;
    public const RadarProcessingRetainedPayloadStrategy RetentionStrategy =
        RadarProcessingRetainedPayloadStrategy.PooledCopy;
    public const RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.AsyncShardTransport;
    public const int WorkerCount = 4;
    public const int WorkerQueueCapacity = 8;
    public const int ProviderQueueCapacity = 8;
    public const long RetainedPayloadBytes = 536_870_912;
    public const bool RetainedPayloadPrewarmEnabled = true;
    public const int RetainedPayloadPrewarmEventCount = 65_536;
    public const int RetainedPayloadPrewarmPayloadBytes = 64 * 1024 * 1024;
    public const int RetainedPayloadPrewarmBatchCount = 1;

    public static TimeSpan OverlapConsumerDelay => TimeSpan.Zero;

    public static RadarProcessingAsyncExecutionOptions CreateAsyncExecution() =>
        new(workerCount: WorkerCount, queueCapacity: WorkerQueueCapacity);

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
