using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static ArchiveIterationTelemetry RunIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarProcessingPressureSkewOptions pressureSkewOptions,
        RadarProcessingExecutionMode executionMode,
        RadarProcessingAsyncExecutionOptions? asyncExecution,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingAsyncWorkerGroup? workerGroup,
        RadarProcessingArchiveProviderMode providerMode,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        using var processor = new ArchiveRebalanceBatchProcessor(
            sourceUniverse,
            mode,
            partitionCount,
            shardCount,
            hardeningOptions,
            pressureSkewOptions,
            executionMode,
            asyncExecution,
            workerTelemetryRecorder,
            workerGroup);
        if (providerMode == RadarProcessingArchiveProviderMode.BlockingBorrowed)
        {
            var publishResult = archiveSession.PublishFile(filePath, processor, cancellationToken);
            return processor.BuildTelemetry(publishResult);
        }

        var queuedResult = providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? PublishFileQueuedOwnedOverlap(
                archiveSession,
                filePath,
                processor,
                queueCapacity,
                queueTimeout,
                retentionStrategy,
                queueRetainedPayloadBytes,
                overlapConsumerDelay,
                retainedPayloadFactory,
                cancellationToken)
            : PublishFileQueuedOwned(
                archiveSession,
                filePath,
                processor,
                queueCapacity,
                queueTimeout,
                retentionStrategy,
                queueRetainedPayloadBytes,
                retainedPayloadFactory,
                cancellationToken);
        return processor
            .BuildTelemetry(queuedResult.PublishResult)
            .WithQueueTelemetry(queuedResult.QueueTelemetry)
            .WithRetentionTelemetry(queuedResult.RetentionTelemetry)
            .WithOverlapTelemetry(queuedResult.OverlapTelemetry);
    }
}
