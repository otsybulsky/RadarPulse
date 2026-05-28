using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
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
}
