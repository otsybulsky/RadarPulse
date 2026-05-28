using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private OrderedProcessingIterationTelemetry RunFileIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        FileInfo fileInfo,
        RadarSourceUniverse sourceUniverse,
        int partitionCount,
        int shardCount,
        int activeBatchCapacity,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        CancellationToken cancellationToken)
    {
        var handlers = RadarProcessingBenchmarkHandlers.Create(handlerSet);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            sourceUniverse,
            partitionCount,
            shardCount,
            handlers: handlers);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var result = RunOrderedProcessing(
                (publisher, token) => archiveSession.PublishFile(fileInfo.FullName, publisher, token),
                core,
                runner,
                activeBatchCapacity,
                cancellationToken);

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        var publishResult = result.Producer.PublishResult ??
                            throw new InvalidOperationException("Archive ordered processing producer did not publish a result.");
        var totals = CacheIterationTotals.Empty;
        totals.ExaminedFiles = 1;
        totals.Add(publishResult);
        return OrderedProcessingIterationTelemetry.FromResult(totals, result);
    }

    private OrderedProcessingIterationTelemetry RunCacheIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        RadarSourceUniverse sourceUniverse,
        int partitionCount,
        int shardCount,
        int activeBatchCapacity,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        CancellationToken cancellationToken)
    {
        var selection = SelectCacheArchiveFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        var publishedTotals = selection.Totals;
        var handlers = RadarProcessingBenchmarkHandlers.Create(handlerSet);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            sourceUniverse,
            partitionCount,
            shardCount,
            handlers: handlers);
        var result = RunOrderedProcessing(
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
                    return lastPublishResult is null
                        ? CreateEmptyCacheAggregatePublishResult(directoryInfo.FullName, sourceUniverse, archiveSession.DegreeOfParallelism)
                        : CreateCacheAggregatePublishResult(directoryInfo.FullName, totals, lastPublishResult);
                },
                core,
                runner,
                activeBatchCapacity,
                cancellationToken);

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        return OrderedProcessingIterationTelemetry.FromResult(publishedTotals, result);
    }

    private static RadarProcessingArchiveQueuedOverlapResult RunOrderedProcessing(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingCore core,
        RadarProcessingArchiveQueuedOverlapRunner runner,
        int activeBatchCapacity,
        CancellationToken cancellationToken)
    {
        var orderedOptions = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity);
        if (core.Options.Handlers.Count == 0)
        {
            return runner.RunProcessingAsync(
                    produce,
                    core,
                    orderedOptions,
                    cancellationToken: cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        return runner.RunMvpProcessingAsync(
                produce,
                core,
                orderedOptions,
                cancellationToken: cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult()
            .OverlapResult;
    }
}
