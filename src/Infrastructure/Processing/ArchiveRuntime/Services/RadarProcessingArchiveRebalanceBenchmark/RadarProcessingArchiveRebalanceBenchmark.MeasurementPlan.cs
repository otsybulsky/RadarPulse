using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static ArchiveBenchmarkMeasurementPlan CreateArchiveBenchmarkMeasurementPlan(
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions,
        RadarProcessingPressureSkewOptions? pressureSkewOptions,
        RadarProcessingExecutionMode? executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingArchiveProviderMode? providerMode,
        int? queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingQueuedProviderOverlapMode? providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy? retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory)
    {
        EnsureKnownMode(mode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);
        ValidateQueueTimeout(queueTimeout);
        ValidateQueueRetainedPayloadBytes(queueRetainedPayloadBytes);
        ValidateOverlapConsumerDelay(overlapConsumerDelay);

        var useRolloutDefaults = !providerMode.HasValue;
        var effectiveProviderMode = providerMode ?? RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode;
        var effectiveExecutionMode = executionMode ??
                                     (useRolloutDefaults
                                         ? RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode
                                         : RadarProcessingExecutionMode.PartitionedBarrier);
        var effectiveQueueCapacity = queueCapacity ??
                                     (useRolloutDefaults
                                         ? RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity
                                         : 1);
        var effectiveProviderOverlapMode = providerOverlapMode ??
                                           (useRolloutDefaults
                                               ? RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode
                                               : RadarProcessingQueuedProviderOverlapMode.None);
        var effectiveRetentionStrategy = retentionStrategy ??
                                         (useRolloutDefaults
                                             ? RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy
                                             : RadarProcessingRetainedPayloadStrategy.SnapshotCopy);
        var effectiveQueueRetainedPayloadBytes = queueRetainedPayloadBytes ??
                                                 (useRolloutDefaults
                                                     ? RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes
                                                     : null);

        EnsureKnownExecutionMode(effectiveExecutionMode);
        EnsureKnownProviderMode(effectiveProviderMode);
        EnsureKnownProviderOverlapMode(effectiveProviderOverlapMode);
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(effectiveRetentionStrategy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveQueueCapacity);
        ValidateQueuedProviderControls(
            effectiveProviderMode,
            effectiveProviderOverlapMode,
            effectiveRetentionStrategy,
            effectiveQueueRetainedPayloadBytes,
            overlapConsumerDelay);

        var effectiveHardeningOptions = hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default;
        var effectiveAsyncExecution = effectiveExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? asyncExecution ?? (useRolloutDefaults
                ? RadarProcessingArchiveRebalanceRolloutDefaults.CreateAsyncExecution()
                : new RadarProcessingAsyncExecutionOptions(workerCount: shardCount, queueCapacity: 1))
            : asyncExecution;
        var defaultRetainedPayloadPrewarm = CreateDefaultRetainedPayloadPrewarm(
            effectiveProviderMode,
            effectiveProviderOverlapMode,
            effectiveRetentionStrategy,
            effectiveExecutionMode,
            effectiveAsyncExecution,
            effectiveQueueCapacity,
            effectiveQueueRetainedPayloadBytes,
            overlapConsumerDelay,
            retainedPayloadFactory);

        return new ArchiveBenchmarkMeasurementPlan(
            effectiveHardeningOptions,
            pressureSkewOptions ?? RadarProcessingPressureSkewOptions.None,
            effectiveExecutionMode,
            effectiveAsyncExecution,
            effectiveProviderMode,
            effectiveQueueCapacity,
            queueTimeout,
            effectiveProviderOverlapMode,
            effectiveRetentionStrategy,
            effectiveQueueRetainedPayloadBytes,
            overlapConsumerDelay,
            defaultRetainedPayloadPrewarm?.Factory ?? retainedPayloadFactory,
            defaultRetainedPayloadPrewarm);
    }

    private sealed record ArchiveBenchmarkMeasurementPlan(
        RadarProcessingRebalanceHardeningOptions HardeningOptions,
        RadarProcessingPressureSkewOptions PressureSkewOptions,
        RadarProcessingExecutionMode ExecutionMode,
        RadarProcessingAsyncExecutionOptions? AsyncExecution,
        RadarProcessingArchiveProviderMode ProviderMode,
        int QueueCapacity,
        TimeSpan? QueueTimeout,
        RadarProcessingQueuedProviderOverlapMode ProviderOverlapMode,
        RadarProcessingRetainedPayloadStrategy RetentionStrategy,
        long? QueueRetainedPayloadBytes,
        TimeSpan OverlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? RetainedPayloadFactory,
        DefaultRetainedPayloadPrewarm? DefaultRetainedPayloadPrewarm);
}
