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
public sealed partial class RadarProcessingArchiveQueuedOverlapRunner
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

}
