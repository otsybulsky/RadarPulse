using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
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
}
