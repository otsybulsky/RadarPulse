using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingArchiveQueuedOverlapRunner
{
    public ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunRebalanceAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(rebalanceSession);

        return RunAsync(
            produce,
            (queue, token) => DrainRebalanceAsync(rebalanceSession, queue, token),
            options,
            cancellationToken);
    }

    public async ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        Func<RadarProcessingOwnedBatchQueue, CancellationToken, ValueTask<RadarProcessingQueuedSessionResult>> consume,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(consume);

        var effectiveOptions = options ?? RadarProcessingArchiveQueuedOverlapOptions.Default;
        var started = Stopwatch.GetTimestamp();
        using var queue = new RadarProcessingOwnedBatchQueue(effectiveOptions.QueueOptions);
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);

        var consumerTask = RunConsumerAsync(queue, consume, cancellationToken).AsTask();
        var producerTask = RunProducerAsync(publisher, produce, cancellationToken).AsTask();

        await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);

        var producer = await producerTask.ConfigureAwait(false);
        var consumer = await consumerTask.ConfigureAwait(false);
        var status = DetermineStatus(producer, consumer);
        var message = DetermineMessage(status, producer, consumer);
        var telemetry = consumer.SessionResult.Telemetry;
        if (ReferenceEquals(telemetry, RadarProcessingProviderQueueTelemetrySummary.Empty))
        {
            telemetry = producer.ProviderResult.Telemetry;
        }

        return new RadarProcessingArchiveQueuedOverlapResult(
            status,
            producer,
            consumer,
            telemetry,
            Stopwatch.GetElapsedTime(started),
            message);
    }

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
            publisher.Queue.Close();
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
        Func<RadarProcessingOwnedBatchQueue, CancellationToken, ValueTask<RadarProcessingQueuedSessionResult>> consume,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var sessionResult = await consume(queue, cancellationToken).ConfigureAwait(false) ??
                                throw new InvalidOperationException("Archive overlap consumer returned null.");
            return new RadarProcessingArchiveQueuedOverlapConsumerResult(
                sessionResult,
                Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            queue.Close();
            return CreateConsumerResult(
                queue,
                RadarProcessingQueuedSessionStatus.Canceled,
                "Archive overlap consumer was canceled.",
                Stopwatch.GetElapsedTime(started));
        }
        catch (ObjectDisposedException exception)
        {
            return CreateConsumerResult(
                queue,
                RadarProcessingQueuedSessionStatus.Disposed,
                exception.Message,
                Stopwatch.GetElapsedTime(started));
        }
        catch (Exception exception)
        {
            queue.Fault(exception.Message);
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

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainRebalanceAsync(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOwnedBatchQueue queue,
        CancellationToken cancellationToken)
    {
        RadarProcessingAsyncRebalanceSession? asyncRebalanceSession = null;
        var ownsAsyncRebalanceSession = rebalanceSession.Core.Options.ExecutionMode ==
            RadarProcessingExecutionMode.AsyncShardTransport;
        if (ownsAsyncRebalanceSession)
        {
            asyncRebalanceSession = new RadarProcessingAsyncRebalanceSession(rebalanceSession);
        }

        await using var queuedSession = new RadarProcessingQueuedRebalanceSession(
            rebalanceSession,
            queue,
            asyncRebalanceSession,
            ownsQueue: false,
            ownsAsyncRebalanceSession: ownsAsyncRebalanceSession);
        return await queuedSession.DrainAsync(cancellationToken).ConfigureAwait(false);
    }
}
