using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunner
{
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

}
