using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Drains an owned provider queue through a rebalance session.
/// </summary>
/// <remarks>
/// The session couples queued provider ownership with rebalance processing so
/// topology changes, migration validation, and queue terminal states are all
/// represented in ordered queued-session evidence.
/// </remarks>
public sealed partial class RadarProcessingQueuedRebalanceSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
    private readonly bool ownsQueue;
    private readonly bool ownsAsyncRebalanceSession;
    private readonly Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private TimeSpan totalDrainTime;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a rebalance session that owns its queue and any required async rebalance session.
    /// </summary>
    public RadarProcessingQueuedRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingProviderQueueOptions? queueOptions = null,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
        : this(
            rebalanceSession,
            new RadarProcessingOwnedBatchQueue(queueOptions),
            CreateAsyncRebalanceSessionIfNeeded(rebalanceSession),
            ownsQueue: true,
            ownsAsyncRebalanceSession: RequiresAsyncRebalanceSession(rebalanceSession),
            consumerResourceLeaseFactory: consumerResourceLeaseFactory)
    {
    }

    /// <summary>
    /// Creates a rebalance session over supplied queue and optional async dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires an async rebalance session wrapping the
    /// same rebalance session. Synchronous mode rejects async dependencies.
    /// </remarks>
    public RadarProcessingQueuedRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOwnedBatchQueue queue,
        RadarProcessingAsyncRebalanceSession? asyncRebalanceSession = null,
        bool ownsQueue = false,
        bool ownsAsyncRebalanceSession = false,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        ArgumentNullException.ThrowIfNull(queue);

        if (rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncRebalanceSession);
            if (!ReferenceEquals(rebalanceSession, asyncRebalanceSession.RebalanceSession))
            {
                throw new ArgumentException(
                    "Queued async rebalance requires the async rebalance session to wrap the supplied rebalance session.",
                    nameof(asyncRebalanceSession));
            }
        }
        else if (asyncRebalanceSession is not null)
        {
            throw new ArgumentException(
                "Queued synchronous rebalance must not carry an async rebalance session.",
                nameof(asyncRebalanceSession));
        }

        this.rebalanceSession = rebalanceSession;
        this.queue = queue;
        this.asyncRebalanceSession = asyncRebalanceSession;
        this.ownsQueue = ownsQueue;
        this.ownsAsyncRebalanceSession = ownsAsyncRebalanceSession;
        this.consumerResourceLeaseFactory = consumerResourceLeaseFactory;
    }

    /// <summary>
    /// Rebalance session that owns topology policy, migration, and processing state.
    /// </summary>
    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    /// <summary>
    /// Processing core used by the rebalance session.
    /// </summary>
    public RadarProcessingCore Core => rebalanceSession.Core;

    /// <summary>
    /// Current topology after any committed rebalance migrations.
    /// </summary>
    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    /// <summary>
    /// Owned provider queue drained by this session.
    /// </summary>
    public RadarProcessingOwnedBatchQueue Queue => queue;

    /// <summary>
    /// Enqueues an owned radar batch and records the enqueue result in session evidence.
    /// </summary>
    public async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var result = await queue.EnqueueAsync(
            batch,
            ownedSnapshotTime,
            ownedSnapshotAllocatedBytes,
            cancellationToken).ConfigureAwait(false);
        RecordEnqueueResult(result);
        return result;
    }

    /// <summary>
    /// Closes the provider queue to new batches while allowing accepted batches to drain.
    /// </summary>
    public void CompleteAdding() => queue.Close();

    /// <summary>
    /// Faults the session and queue so later accepted batches are skipped after fault.
    /// </summary>
    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        MarkFaulted(message);
    }

    /// <summary>
    /// Drains the queue sequentially and rebalance-processes each accepted batch.
    /// </summary>
    /// <returns>
    /// A queued session result containing enqueue evidence, processing or
    /// rebalance outcomes, queue telemetry, terminal status, and final topology version.
    /// </returns>
    public async ValueTask<RadarProcessingQueuedSessionResult> DrainAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var started = Stopwatch.GetTimestamp();
        try
        {
            while (true)
            {
                var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                switch (dequeue.Status)
                {
                    case RadarProcessingOwnedBatchDequeueStatus.Item:
                        await ProcessDequeuedBatchAsync(dequeue.Batch!, cancellationToken).ConfigureAwait(false);
                        break;

                    case RadarProcessingOwnedBatchDequeueStatus.Closed:
                        AddDrainTime(started);
                        return CreateSessionResult(GetTerminalStatus(), GetTerminalMessage());

                    case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                        MarkFaulted(dequeue.Message);
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Faulted, dequeue.Message);

                    case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                        MarkCanceledAndRecordQueued();
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");

                    case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Disposed, "Queued rebalance queue was disposed.");

                    default:
                        RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                        throw new ArgumentOutOfRangeException(nameof(dequeue));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceledAndRecordQueued();
            AddDrainTime(started);
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");
        }
    }

    /// <summary>
    /// Computes rebalance batches concurrently while committing results by provider sequence.
    /// </summary>
    /// <remarks>
    /// Ordered commit may recompute stale deltas when an earlier committed
    /// migration changes topology before a later batch is published.
    /// </remarks>
    public async ValueTask<RadarProcessingQueuedSessionResult> DrainOrderedConcurrentAsync(
        RadarProcessingOrderedConcurrencyOptions? orderedOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var effectiveOptions = orderedOptions ?? RadarProcessingOrderedConcurrencyOptions.Default;
        if (effectiveOptions.IsSequential)
        {
            return await DrainAsync(cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        using var activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var active = new List<OrderedConcurrentRebalanceBatchWork>(effectiveOptions.ActiveBatchCapacity);
        var completed = new List<OrderedConcurrentRebalanceBatchCompletion>(effectiveOptions.ActiveBatchCapacity);
        var inputClosed = false;
        var nextPublishSequence = -1L;

        try
        {
            while (!inputClosed || active.Count > 0 || completed.Count > 0)
            {
                while (!inputClosed && active.Count < effectiveOptions.ActiveBatchCapacity)
                {
                    var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                    switch (dequeue.Status)
                    {
                        case RadarProcessingOwnedBatchDequeueStatus.Item:
                            var queuedBatch = dequeue.Batch!;
                            if (nextPublishSequence < 0)
                            {
                                nextPublishSequence = queuedBatch.Sequence.Value;
                            }

                            if (IsFaulted)
                            {
                                completed.Add(CreateSkippedAfterFaultCompletion(queuedBatch));
                            }
                            else
                            {
                                active.Add(StartOrderedConcurrentBatch(queuedBatch, activeCancellation.Token));
                            }

                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Closed:
                            inputClosed = true;
                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                            MarkFaulted(dequeue.Message);
                            inputClosed = true;
                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                            activeCancellation.Cancel();
                            MarkCanceledAndRecordQueued();
                            inputClosed = true;
                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                            AddDrainTime(started);
                            return CreateSessionResult(
                                RadarProcessingQueuedSessionStatus.Disposed,
                                "Queued rebalance queue was disposed.");

                        default:
                            RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                            throw new ArgumentOutOfRangeException(nameof(dequeue));
                    }
                }

                PublishReadyOrderedCompletions(completed, ref nextPublishSequence, activeCancellation);
                if (active.Count == 0)
                {
                    if (inputClosed && completed.Count == 0)
                    {
                        break;
                    }

                    continue;
                }

                var completedTask = await Task.WhenAny(active.Select(static item => item.Task)).ConfigureAwait(false);
                var activeIndex = FindActiveWorkIndex(active, completedTask);
                var activeWork = active[activeIndex];
                active.RemoveAt(activeIndex);
                completed.Add(await activeWork.Task.ConfigureAwait(false));
                PublishReadyOrderedCompletions(completed, ref nextPublishSequence, activeCancellation);
            }

            AddDrainTime(started);
            return CreateSessionResult(GetTerminalStatus(), GetTerminalMessage());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activeCancellation.Cancel();
            await CompleteAndDiscardActiveWorkAsync(active).ConfigureAwait(false);
            MarkCanceledAndRecordQueued();
            AddDrainTime(started);
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");
        }
        finally
        {
            DisposeCompleted(completed);
        }
    }

    /// <summary>
    /// Synchronously disposes owned queue and async rebalance resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes owned queue and async rebalance resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        bool shouldDispose;
        lock (sync)
        {
            shouldDispose = !disposed;
            disposed = true;
        }

        if (!shouldDispose)
        {
            return;
        }

        if (ownsAsyncRebalanceSession && asyncRebalanceSession is not null)
        {
            await asyncRebalanceSession.DisposeAsync().ConfigureAwait(false);
        }

        if (ownsQueue)
        {
            queue.Dispose();
        }
    }

    private bool IsDisposed
    {
        get
        {
            lock (sync)
            {
                return disposed;
            }
        }
    }

    private async ValueTask ProcessDequeuedBatchAsync(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        try
        {
            using var consumerResourceLease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
            if (IsFaulted)
            {
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        queuedBatch.Sequence,
                        faultMessage));
                return;
            }

            var rebalanceResult = asyncRebalanceSession is null
                ? rebalanceSession.Process(queuedBatch.Batch, cancellationToken)
                : await asyncRebalanceSession.ProcessAsync(queuedBatch.Batch, cancellationToken).ConfigureAwait(false);

            var processingResult = rebalanceResult.ProcessingResult;
            if (!processingResult.IsValid)
            {
                var message = processingResult.Validation.Message;
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        message,
                        processingResult));
                MarkFaulted(message);
                return;
            }

            if (rebalanceResult.MigrationResult is { Succeeded: false } migrationResult)
            {
                var message = $"Queued rebalance migration failed with state {migrationResult.State}.";
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedMigration(
                        queuedBatch.Sequence,
                        message,
                        rebalanceResult));
                MarkFaulted(message);
                return;
            }

            if (!rebalanceResult.Validation.IsValid)
            {
                var message = rebalanceResult.Validation.Message;
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        message,
                        processingResult));
                MarkFaulted(message);
                return;
            }

            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    queuedBatch.Sequence,
                    rebalanceResult));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled."));
            MarkCanceledAndRecordQueued();
        }
        catch (Exception exception)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message));
            MarkFaulted(exception.Message);
        }
    }
}
