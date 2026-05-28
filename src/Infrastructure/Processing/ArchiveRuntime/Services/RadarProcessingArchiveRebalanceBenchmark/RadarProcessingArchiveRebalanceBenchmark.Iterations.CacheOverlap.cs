using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static QueuedArchiveCachePublishResult PublishCacheQueuedOwnedOverlap(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        ArchiveRebalanceBatchProcessor processor,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        var selection = SelectCacheArchiveFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        if (selection.BaseDataFiles.Count == 0)
        {
            return new QueuedArchiveCachePublishResult(
                selection.Totals,
                RadarProcessingProviderQueueTelemetrySummary.Empty,
                new RadarProcessingRetainedPayloadTelemetrySummary(retentionStrategy),
                new RadarProcessingArchiveOverlapTelemetrySummary(retentionStrategy));
        }

        var publishedTotals = selection.Totals;
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var result = runner.RunAsync(
                (publisher, token) =>
                {
                    var totals = selection.Totals;
                    ArchiveRadarEventBatchPublishResult? lastPublishResult = null;
                    foreach (var fileInfo in selection.BaseDataFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        var publishResult = archiveSession.PublishFile(fileInfo.FullName, publisher, token);
                        totals.Add(publishResult);
                        lastPublishResult = publishResult;
                    }

                    publishedTotals = totals;
                    return CreateCacheAggregatePublishResult(
                        directoryInfo.FullName,
                        totals,
                        lastPublishResult ?? throw new InvalidOperationException("Cache overlap producer did not publish any archive files."));
                },
                (queue, publisher, token) => DrainQueueToProcessorAsync(
                    queue,
                    publisher,
                    processor,
                    overlapConsumerDelay,
                    token),
                new RadarProcessingArchiveQueuedOverlapOptions(
                    new RadarProcessingProviderQueueOptions(
                        capacity: queueCapacity,
                        enqueueTimeout: queueTimeout,
                        maxRetainedPayloadBytes: queueRetainedPayloadBytes),
                    new RadarProcessingRetainedPayloadOptions(
                        retentionStrategy,
                        queueRetainedPayloadBytes),
                    retainedPayloadFactory),
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        return new QueuedArchiveCachePublishResult(
            publishedTotals,
            result.QueueTelemetry,
            result.OverlapTelemetry.RetentionTelemetry,
            result.OverlapTelemetry);
    }
}
