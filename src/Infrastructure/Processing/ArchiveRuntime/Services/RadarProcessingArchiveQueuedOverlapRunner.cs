using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Runs archive publishing and queued processing concurrently over an owned provider queue.
/// </summary>
public sealed class RadarProcessingArchiveQueuedOverlapRunner
{
    /// <summary>
    /// Runs archive publishing into a queued rebalance consumer.
    /// </summary>
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
            (queue, publisher, token) => DrainRebalanceAsync(rebalanceSession, queue, publisher, token),
            options,
            cancellationToken);
    }

    /// <summary>
    /// Runs archive publishing into an ordered concurrent queued rebalance consumer.
    /// </summary>
    public ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunOrderedRebalanceAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOrderedConcurrencyOptions? orderedConcurrencyOptions = null,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        var effectiveOrderedConcurrencyOptions =
            orderedConcurrencyOptions ?? RadarProcessingRuntimeArchiveBaseline.OrderedConcurrencyOptions;

        return RunAsync(
            produce,
            (queue, publisher, token) => DrainOrderedRebalanceAsync(
                rebalanceSession,
                queue,
                publisher,
                effectiveOrderedConcurrencyOptions,
                token),
            CreateOrderedProcessingOverlapOptions(
                options ?? RadarProcessingArchiveQueuedOverlapOptions.Default,
                effectiveOrderedConcurrencyOptions),
            cancellationToken);
    }

    /// <summary>
    /// Runs archive publishing into an ordered queued processing consumer.
    /// </summary>
    public ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunProcessingAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingCore core,
        RadarProcessingOrderedConcurrencyOptions? orderedConcurrencyOptions = null,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(core);
        var effectiveOrderedConcurrencyOptions =
            orderedConcurrencyOptions ?? RadarProcessingRuntimeArchiveBaseline.OrderedConcurrencyOptions;

        return RunAsync(
            produce,
            (queue, publisher, token) => DrainProcessingAsync(
                core,
                queue,
                publisher,
                effectiveOrderedConcurrencyOptions,
                token),
            CreateOrderedProcessingOverlapOptions(
                options ?? RadarProcessingArchiveQueuedOverlapOptions.Default,
                effectiveOrderedConcurrencyOptions),
            cancellationToken);
    }

    /// <summary>
    /// Runs MVP processing using the handler-aware runtime plan.
    /// </summary>
    public async ValueTask<RadarProcessingMvpRuntimeResult> RunMvpProcessingAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingCore core,
        RadarProcessingOrderedConcurrencyOptions? orderedConcurrencyOptions = null,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(core);

        var plan = RadarProcessingMvpRuntimePlan.Create(core, orderedConcurrencyOptions);
        if (plan.HandlerOutputContract.IsUnsupported)
        {
            throw new NotSupportedException(plan.Message);
        }

        var result = plan.AllowsOrderedConcurrentHandlerDeltaMerge
            ? await RunHandlerDeltaMergeProcessingAsync(
                    produce,
                    core,
                    plan.EffectiveOrderedConcurrencyOptions,
                    options,
                    cancellationToken)
                .ConfigureAwait(false)
            : await RunProcessingAsync(
                    produce,
                    core,
                    plan.EffectiveOrderedConcurrencyOptions,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);
        return new RadarProcessingMvpRuntimeResult(plan, result);
    }

    /// <summary>
    /// Runs producer/consumer overlap with a queue-only consumer delegate.
    /// </summary>
    public async ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        Func<RadarProcessingOwnedBatchQueue, CancellationToken, ValueTask<RadarProcessingQueuedSessionResult>> consume,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await RunAsync(
                produce,
                (queue, _, token) => consume(queue, token),
                options,
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Runs producer/consumer overlap with access to the queueing publisher.
    /// </summary>
    public async ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        Func<RadarProcessingOwnedBatchQueue, ArchiveOwnedRadarEventBatchQueueingPublisher, CancellationToken, ValueTask<RadarProcessingQueuedSessionResult>> consume,
        RadarProcessingArchiveQueuedOverlapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(consume);

        var effectiveOptions = options ?? RadarProcessingArchiveQueuedOverlapOptions.Default;
        var prewarm = ApplyStartupRetainedPayloadPrewarm(effectiveOptions);
        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var started = Stopwatch.GetTimestamp();
        using var queue = new RadarProcessingOwnedBatchQueue(effectiveOptions.QueueOptions);
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: effectiveOptions.RetainedPayloadOptions,
            retainedPayloadFactory: prewarm.Factory);

        var consumerTask = RunConsumerAsync(queue, publisher, consume, cancellationToken).AsTask();
        var producerTask = RunProducerAsync(publisher, produce, cancellationToken).AsTask();

        await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);

        var producer = await producerTask.ConfigureAwait(false);
        var consumer = await consumerTask.ConfigureAwait(false);
        publisher.ReleasePendingResources();
        producer = CreateProducerResultWithFinalProviderTelemetry(producer, publisher.CreateResult());
        var status = DetermineStatus(producer, consumer);
        var message = DetermineMessage(status, producer, consumer);
        var providerTelemetry = producer.ProviderResult.Telemetry;
        var telemetry = consumer.SessionResult.Telemetry;
        if (ReferenceEquals(telemetry, RadarProcessingProviderQueueTelemetrySummary.Empty))
        {
            telemetry = providerTelemetry;
        }
        else
        {
            telemetry = telemetry.WithRetainedResourcePressure(providerTelemetry.RetainedResourcePressure);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        var measuredAllocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var overlapTelemetry = RadarProcessingArchiveOverlapTelemetrySummary.FromOverlapResult(
            producer,
            consumer,
            telemetry,
            elapsed,
            measuredAllocatedBytes);

        return new RadarProcessingArchiveQueuedOverlapResult(
            status,
            producer,
            consumer,
            telemetry,
            overlapTelemetry,
            elapsed,
            message,
            prewarm.Result);
    }

    private static RetainedPayloadPrewarmLifecycle ApplyStartupRetainedPayloadPrewarm(
        RadarProcessingArchiveQueuedOverlapOptions options)
    {
        if (!options.RetainedPayloadPrewarmOptions.Enabled)
        {
            return new RetainedPayloadPrewarmLifecycle(
                options.RetainedPayloadFactory,
                RadarProcessingRetainedPayloadPrewarmResult.None);
        }

        var factory = options.RetainedPayloadFactory ?? new RadarProcessingRetainedPayloadFactory();
        var prewarm = factory.Prewarm(
            options.RetainedPayloadPrewarmOptions.EventCount,
            options.RetainedPayloadPrewarmOptions.PayloadBytes,
            options.RetainedPayloadPrewarmOptions.RetainedBatchCount);
        return new RetainedPayloadPrewarmLifecycle(factory, prewarm);
    }

    private static RadarProcessingArchiveQueuedOverlapOptions CreateOrderedProcessingOverlapOptions(
        RadarProcessingArchiveQueuedOverlapOptions options,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(orderedConcurrencyOptions);

        var prewarm = options.RetainedPayloadPrewarmOptions;
        if (!prewarm.Enabled ||
            prewarm.RetainedBatchCount >= orderedConcurrencyOptions.ActiveBatchCapacity)
        {
            return options;
        }

        var retainedPayloadFactory = options.RetainedPayloadFactory ??
            CreateOrderedProcessingRetainedPayloadFactory(
                prewarm,
                orderedConcurrencyOptions.ActiveBatchCapacity);

        return new RadarProcessingArchiveQueuedOverlapOptions(
            options.QueueOptions,
            options.RetainedPayloadOptions,
            retainedPayloadFactory,
            new RadarProcessingRetainedPayloadPrewarmOptions(
                prewarm.EventCount,
                prewarm.PayloadBytes,
                orderedConcurrencyOptions.ActiveBatchCapacity));
    }

    private static RadarProcessingRetainedPayloadFactory CreateOrderedProcessingRetainedPayloadFactory(
        RadarProcessingRetainedPayloadPrewarmOptions prewarm,
        int activeBatchCapacity)
    {
        var retainedEventBytes = Math.Max(
            RadarProcessingRetainedEventArrayPool.DefaultMaxRetainedBytes,
            checked((long)prewarm.EventCount * RadarStreamEvent.SizeInBytes * activeBatchCapacity));
        var retainedPayloadBytes = Math.Max(
            RadarProcessingRetainedPayloadByteArrayPool.DefaultMaxRetainedBytes,
            checked((long)prewarm.PayloadBytes * activeBatchCapacity));
        return new RadarProcessingRetainedPayloadFactory(
            new RadarProcessingRetainedEventArrayPool(
                maxRetainedArrayCount: Math.Max(
                    RadarProcessingRetainedEventArrayPool.DefaultMaxRetainedArrayCount,
                    activeBatchCapacity),
                maxRetainedBytes: retainedEventBytes),
            new RadarProcessingRetainedPayloadByteArrayPool(
                maxRetainedArrayCount: Math.Max(
                    RadarProcessingRetainedPayloadByteArrayPool.DefaultMaxRetainedArrayCount,
                    activeBatchCapacity),
                maxRetainedBytes: retainedPayloadBytes));
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

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainRebalanceAsync(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
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
            ownsAsyncRebalanceSession: ownsAsyncRebalanceSession,
            consumerResourceLeaseFactory: publisher.AcquireConsumerResourceLease);
        var result = await queuedSession.DrainAsync(cancellationToken).ConfigureAwait(false);
        publisher.ReleasePendingResources();
        return result;
    }

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainOrderedRebalanceAsync(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions,
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
            ownsAsyncRebalanceSession: ownsAsyncRebalanceSession,
            consumerResourceLeaseFactory: publisher.AcquireConsumerResourceLease);
        var result = await queuedSession
            .DrainOrderedConcurrentAsync(orderedConcurrencyOptions, cancellationToken)
            .ConfigureAwait(false);
        publisher.ReleasePendingResources();
        return result;
    }

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainProcessingAsync(
        RadarProcessingCore core,
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions,
        CancellationToken cancellationToken)
    {
        RadarProcessingAsyncCoreSession? asyncCoreSession = null;
        var ownsAsyncCoreSession = core.Options.ExecutionMode ==
            RadarProcessingExecutionMode.AsyncShardTransport;
        if (ownsAsyncCoreSession)
        {
            asyncCoreSession = new RadarProcessingAsyncCoreSession(core);
        }

        await using var queuedSession = new RadarProcessingQueuedProcessingSession(
            core,
            queue,
            asyncCoreSession,
            ownsQueue: false,
            ownsAsyncCoreSession: ownsAsyncCoreSession,
            consumerResourceLeaseFactory: publisher.AcquireConsumerResourceLease);
        var result = await queuedSession
            .DrainOrderedConcurrentAsync(orderedConcurrencyOptions, cancellationToken)
            .ConfigureAwait(false);
        publisher.ReleasePendingResources();
        return result;
    }

    private ValueTask<RadarProcessingArchiveQueuedOverlapResult> RunHandlerDeltaMergeProcessingAsync(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingCore core,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions,
        RadarProcessingArchiveQueuedOverlapOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(orderedConcurrencyOptions);

        return RunAsync(
            produce,
            (queue, publisher, token) => DrainHandlerDeltaMergeProcessingAsync(
                core,
                queue,
                publisher,
                orderedConcurrencyOptions,
                token),
            CreateOrderedProcessingOverlapOptions(
                options ?? RadarProcessingArchiveQueuedOverlapOptions.Default,
                orderedConcurrencyOptions),
            cancellationToken);
    }

    private static async ValueTask<RadarProcessingQueuedSessionResult> DrainHandlerDeltaMergeProcessingAsync(
        RadarProcessingCore core,
        RadarProcessingOwnedBatchQueue queue,
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        RadarProcessingOrderedConcurrencyOptions orderedConcurrencyOptions,
        CancellationToken cancellationToken)
    {
        RadarProcessingAsyncCoreSession? asyncCoreSession = null;
        var ownsAsyncCoreSession = core.Options.ExecutionMode ==
            RadarProcessingExecutionMode.AsyncShardTransport;
        if (ownsAsyncCoreSession)
        {
            asyncCoreSession = new RadarProcessingAsyncCoreSession(core);
        }

        await using var queuedSession = new RadarProcessingQueuedProcessingSession(
            core,
            queue,
            asyncCoreSession,
            ownsQueue: false,
            ownsAsyncCoreSession,
            consumerResourceLeaseFactory: publisher.AcquireConsumerResourceLease);
        var result = await queuedSession
            .DrainOrderedHandlerDeltaMergeAsync(orderedConcurrencyOptions, cancellationToken)
            .ConfigureAwait(false);
        publisher.ReleasePendingResources();
        return result;
    }

    private sealed record RetainedPayloadPrewarmLifecycle(
        RadarProcessingRetainedPayloadFactory? Factory,
        RadarProcessingRetainedPayloadPrewarmResult Result);
}
