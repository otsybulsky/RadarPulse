using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunner
{
    private static async ValueTask<RadarProcessingArchiveQueuedOverlapProducerResult> RunProducerAsync(
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publishResult = await Task.Run(
                    () => produce(publisher, cancellationToken),
                    CancellationToken.None)
                .ConfigureAwait(false);
            publisher.CompleteAdding();
            return RadarProcessingArchiveQueuedOverlapProducerResult.Completed(
                publishResult,
                publisher.CreateResult(),
                Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ApplyCancellationShutdown(publisher.Queue);
            return RadarProcessingArchiveQueuedOverlapProducerResult.Canceled(
                publisher.CreateResult(),
                Stopwatch.GetElapsedTime(started),
                "Archive overlap producer was canceled.");
        }
        catch (Exception exception)
        {
            publisher.Queue.Fault(exception.Message);
            return RadarProcessingArchiveQueuedOverlapProducerResult.Failed(
                exception.Message,
                publisher.CreateResult(),
                Stopwatch.GetElapsedTime(started));
        }
    }

    private static async ValueTask<RadarProcessingArchiveQueuedOverlapConsumerResult> RunConsumerAsync(
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        Func<RadarProcessingOwnedBatchQueue, ArchiveOwnedRadarEventBatchQueueingPublisher, CancellationToken, ValueTask<RadarProcessingQueuedSessionResult>> consume,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var sessionResult = await consume(queue, publisher, cancellationToken).ConfigureAwait(false) ??
                                throw new InvalidOperationException("Archive overlap consumer returned null.");
            publisher.ReleasePendingResources();
            return new RadarProcessingArchiveQueuedOverlapConsumerResult(
                sessionResult,
                Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ApplyCancellationShutdown(queue);
            publisher.ReleasePendingResources();
            return CreateConsumerResult(
                queue,
                RadarProcessingQueuedSessionStatus.Canceled,
                "Archive overlap consumer was canceled.",
                Stopwatch.GetElapsedTime(started));
        }
        catch (ObjectDisposedException exception)
        {
            publisher.ReleasePendingResources();
            return CreateConsumerResult(
                queue,
                RadarProcessingQueuedSessionStatus.Disposed,
                exception.Message,
                Stopwatch.GetElapsedTime(started));
        }
        catch (Exception exception)
        {
            queue.Fault(exception.Message);
            publisher.ReleasePendingResources();
            return CreateConsumerResult(
                queue,
                RadarProcessingQueuedSessionStatus.Faulted,
                exception.Message,
                Stopwatch.GetElapsedTime(started));
        }
    }

    private static RadarProcessingArchiveQueuedOverlapConsumerResult CreateConsumerResult(
        RadarProcessingOwnedBatchQueue queue,
        RadarProcessingQueuedSessionStatus status,
        string message,
        TimeSpan elapsed) =>
        new(
            new RadarProcessingQueuedSessionResult(
                status,
                queue.CreateTelemetrySummary(),
                message: message),
            elapsed);

    private static void ApplyCancellationShutdown(
        RadarProcessingOwnedBatchQueue queue)
    {
        if (queue.Options.ShutdownMode == RadarProcessingProviderQueueShutdownMode.CancelQueued)
        {
            queue.CancelQueued();
            return;
        }

        queue.Close();
    }

    private static RadarProcessingArchiveQueuedOverlapProducerResult CreateProducerResultWithFinalProviderTelemetry(
        RadarProcessingArchiveQueuedOverlapProducerResult producer,
        RadarProcessingArchiveQueuedProviderResult providerResult) =>
        producer.Status switch
        {
            RadarProcessingArchiveQueuedOverlapProducerStatus.Completed =>
                RadarProcessingArchiveQueuedOverlapProducerResult.Completed(
                    producer.PublishResult!,
                    providerResult,
                    producer.Elapsed),
            RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled =>
                RadarProcessingArchiveQueuedOverlapProducerResult.Canceled(
                    providerResult,
                    producer.Elapsed,
                    producer.Message),
            RadarProcessingArchiveQueuedOverlapProducerStatus.Failed =>
                RadarProcessingArchiveQueuedOverlapProducerResult.Failed(
                    producer.Message,
                    providerResult,
                    producer.Elapsed),
            _ => throw new ArgumentOutOfRangeException(nameof(producer))
        };

    private static RadarProcessingArchiveQueuedOverlapStatus DetermineStatus(
        RadarProcessingArchiveQueuedOverlapProducerResult producer,
        RadarProcessingArchiveQueuedOverlapConsumerResult consumer)
    {
        if (producer.IsCanceled || consumer.IsCanceled)
        {
            return RadarProcessingArchiveQueuedOverlapStatus.Canceled;
        }

        if (consumer.IsDisposed)
        {
            return RadarProcessingArchiveQueuedOverlapStatus.Disposed;
        }

        if (consumer.IsFaulted)
        {
            return RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted;
        }

        if (producer.IsFailed)
        {
            return RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed;
        }

        return producer.IsCompleted && consumer.IsCompleted
            ? RadarProcessingArchiveQueuedOverlapStatus.Completed
            : RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted;
    }

    private static string DetermineMessage(
        RadarProcessingArchiveQueuedOverlapStatus status,
        RadarProcessingArchiveQueuedOverlapProducerResult producer,
        RadarProcessingArchiveQueuedOverlapConsumerResult consumer) =>
        status switch
        {
            RadarProcessingArchiveQueuedOverlapStatus.Completed => string.Empty,
            RadarProcessingArchiveQueuedOverlapStatus.Canceled => !string.IsNullOrEmpty(consumer.Message)
                ? consumer.Message
                : producer.Message,
            RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed => producer.Message,
            RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted => consumer.Message,
            RadarProcessingArchiveQueuedOverlapStatus.Disposed => consumer.Message,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

}
