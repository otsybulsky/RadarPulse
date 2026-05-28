using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static ArchiveIterationTelemetry RunCacheIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
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
        var totals = CacheIterationTotals.Empty;
        var queueTelemetry = RadarProcessingProviderQueueTelemetrySummary.Empty;
        var retentionTelemetry = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned
            ? new RadarProcessingRetainedPayloadTelemetrySummary(retentionStrategy)
            : RadarProcessingRetainedPayloadTelemetrySummary.Empty;
        var overlapTelemetry = providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? new RadarProcessingArchiveOverlapTelemetrySummary(retentionStrategy)
            : RadarProcessingArchiveOverlapTelemetrySummary.Empty;

        if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            var queuedResult = PublishCacheQueuedOwnedOverlap(
                archiveSession,
                directoryInfo,
                date,
                radarId,
                maxFiles,
                processor,
                queueCapacity,
                queueTimeout,
                retentionStrategy,
                queueRetainedPayloadBytes,
                overlapConsumerDelay,
                retainedPayloadFactory,
                cancellationToken);

            return processor
                .BuildTelemetry(queuedResult.Totals)
                .WithQueueTelemetry(queuedResult.QueueTelemetry)
                .WithRetentionTelemetry(queuedResult.RetentionTelemetry)
                .WithOverlapTelemetry(queuedResult.OverlapTelemetry);
        }

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFileCount >= maxFiles)
            {
                break;
            }

            if (!RadarProcessingArchiveBenchmarkCacheSelection.MatchesRadar(fileInfo, radarId) ||
                !RadarProcessingArchiveBenchmarkCacheSelection.MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFileCount++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFileCount++;
                continue;
            }

            ArchiveRadarEventBatchPublishResult publishResult;
            if (providerMode == RadarProcessingArchiveProviderMode.BlockingBorrowed)
            {
                publishResult = archiveSession.PublishFile(fileInfo.FullName, processor, cancellationToken);
            }
            else
            {
                var queuedResult = providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
                    ? PublishFileQueuedOwnedOverlap(
                        archiveSession,
                        fileInfo.FullName,
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
                        fileInfo.FullName,
                        processor,
                        queueCapacity,
                        queueTimeout,
                        retentionStrategy,
                        queueRetainedPayloadBytes,
                        retainedPayloadFactory,
                        cancellationToken);

                publishResult = queuedResult.PublishResult;
                queueTelemetry = AddQueueTelemetry(queueTelemetry, queuedResult.QueueTelemetry);
                retentionTelemetry = AddRetentionTelemetry(retentionTelemetry, queuedResult.RetentionTelemetry);
                overlapTelemetry = AddOverlapTelemetry(overlapTelemetry, queuedResult.OverlapTelemetry);
            }

            totals.Add(publishResult);
        }

        return processor
            .BuildTelemetry(totals)
            .WithQueueTelemetry(queueTelemetry)
            .WithRetentionTelemetry(retentionTelemetry)
            .WithOverlapTelemetry(overlapTelemetry);
    }
}
