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

    private static QueuedArchivePublishResult PublishFileQueuedOwned(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        ArchiveRebalanceBatchProcessor processor,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: queueCapacity,
                enqueueTimeout: queueTimeout,
                maxRetainedPayloadBytes: queueRetainedPayloadBytes));
        using var queueingPublisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                retentionStrategy,
                queueRetainedPayloadBytes),
            retainedPayloadFactory: retainedPayloadFactory);

        var publishResult = archiveSession.PublishFile(filePath, queueingPublisher, cancellationToken);
        queueingPublisher.CompleteAdding();

        var drainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var completed = 0L;
        var failed = 0L;
        var canceled = 0L;
        while (true)
        {
            var dequeue = queue.DequeueAsync(cancellationToken).AsTask().GetAwaiter().GetResult();
            switch (dequeue.Status)
            {
                case RadarProcessingOwnedBatchDequeueStatus.Item:
                    try
                    {
                        var queuedBatch = dequeue.Batch!;
                        using var consumerResourceLease = queueingPublisher.AcquireConsumerResourceLease(queuedBatch.Sequence);
                        processor.Publish(queuedBatch.Batch, cancellationToken);
                        completed++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        canceled++;
                        throw;
                    }
                    catch
                    {
                        failed++;
                        throw;
                    }

                    break;

                case RadarProcessingOwnedBatchDequeueStatus.Closed:
                    var providerResult = queueingPublisher.CreateResult();
                    var queueTelemetry = WithQueueCompletionTelemetry(
                            queue.CreateTelemetrySummary(),
                            completed,
                            failed,
                            canceled,
                            skippedAfterFault: 0,
                            System.Diagnostics.Stopwatch.GetElapsedTime(drainStarted))
                        .WithRetainedResourcePressure(providerResult.Telemetry.RetainedResourcePressure);
                    return new QueuedArchivePublishResult(
                        publishResult,
                        queueTelemetry,
                        providerResult.RetentionTelemetry,
                        RadarProcessingArchiveOverlapTelemetrySummary.Empty);

                case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                    throw new OperationCanceledException(cancellationToken);

                case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                    throw new InvalidOperationException(dequeue.Message);

                case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                    throw new ObjectDisposedException(nameof(RadarProcessingOwnedBatchQueue));

                default:
                    RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                    throw new ArgumentOutOfRangeException(nameof(dequeue));
            }
        }
    }

    private static QueuedArchivePublishResult PublishFileQueuedOwnedOverlap(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        ArchiveRebalanceBatchProcessor processor,
        int queueCapacity,
        TimeSpan? queueTimeout,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory,
        CancellationToken cancellationToken)
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var result = runner.RunAsync(
                (publisher, token) => archiveSession.PublishFile(filePath, publisher, token),
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

        return new QueuedArchivePublishResult(
            result.Producer.PublishResult!,
            result.QueueTelemetry,
            result.OverlapTelemetry.RetentionTelemetry,
            result.OverlapTelemetry);
    }

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainQueueToProcessorAsync(
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        ArchiveRebalanceBatchProcessor processor,
        TimeSpan overlapConsumerDelay,
        CancellationToken cancellationToken)
    {
        var drainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var completed = 0L;
        var failed = 0L;
        var canceled = 0L;
        while (true)
        {
            var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
            switch (dequeue.Status)
            {
                case RadarProcessingOwnedBatchDequeueStatus.Item:
                    var queuedBatch = dequeue.Batch!;
                    try
                    {
                        using var consumerResourceLease = publisher.AcquireConsumerResourceLease(queuedBatch.Sequence);
                        if (overlapConsumerDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(overlapConsumerDelay, cancellationToken).ConfigureAwait(false);
                        }

                        processor.Publish(queuedBatch.Batch, cancellationToken);
                        completed++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        canceled++;
                        throw;
                    }
                    catch
                    {
                        failed++;
                        throw;
                    }

                    break;

                case RadarProcessingOwnedBatchDequeueStatus.Closed:
                    var queueTelemetry = WithQueueCompletionTelemetry(
                        queue.CreateTelemetrySummary(),
                        completed,
                        failed,
                        canceled,
                        skippedAfterFault: 0,
                        System.Diagnostics.Stopwatch.GetElapsedTime(drainStarted));
                    return new RadarProcessingQueuedSessionResult(
                        RadarProcessingQueuedSessionStatus.Completed,
                        queueTelemetry);

                case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                    throw new OperationCanceledException(cancellationToken);

                case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                    throw new InvalidOperationException(dequeue.Message);

                case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                    throw new ObjectDisposedException(nameof(RadarProcessingOwnedBatchQueue));

                default:
                    RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                    throw new ArgumentOutOfRangeException(nameof(dequeue));
            }
        }
    }

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
