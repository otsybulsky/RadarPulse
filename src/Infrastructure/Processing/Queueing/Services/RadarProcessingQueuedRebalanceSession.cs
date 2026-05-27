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
public sealed class RadarProcessingQueuedRebalanceSession : IDisposable, IAsyncDisposable
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

    private OrderedConcurrentRebalanceBatchWork StartOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentRebalanceBatchWork(task);
    }

    private OrderedConcurrentRebalanceBatchCompletion CreateSkippedAfterFaultCompletion(
        RadarProcessingQueuedBatch queuedBatch)
    {
        using var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                queuedBatch.Sequence,
                faultMessage));
    }

    private OrderedConcurrentRebalanceBatchCompletion ComputeOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued rebalance batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = rebalanceSession.Core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            if (asyncRebalanceSession is not null)
            {
                var asyncDelta = asyncRebalanceSession
                    .AsyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                return OrderedConcurrentRebalanceBatchCompletion.FromAsyncDelta(
                    queuedBatch.Sequence,
                    queuedBatch.Batch,
                    asyncDelta,
                    lease);
            }

            var delta = rebalanceSession.Core.ComputeProcessingDelta(queuedBatch.Batch, cancellationToken);
            return OrderedConcurrentRebalanceBatchCompletion.FromDelta(
                queuedBatch.Sequence,
                queuedBatch.Batch,
                delta,
                lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = rebalanceSession.Core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentRebalanceBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private void PublishReadyOrderedCompletions(
        List<OrderedConcurrentRebalanceBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation)
    {
        if (nextPublishSequence < 0)
        {
            return;
        }

        while (true)
        {
            var index = FindCompletionIndex(completed, nextPublishSequence);
            if (index < 0)
            {
                return;
            }

            var completion = completed[index];
            completed.RemoveAt(index);
            try
            {
                var result = IsFaulted
                    ? RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        completion.Sequence,
                        faultMessage)
                    : completion.Commit(
                        rebalanceSession,
                        asyncRebalanceSession,
                        activeCancellation.Token);
                RecordProcessingResult(result);
                nextPublishSequence++;

                if (IsFailedProcessingStatus(result.Status))
                {
                    MarkFaulted(result.Message);
                    activeCancellation.Cancel();
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    activeCancellation.Cancel();
                    MarkCanceledAndRecordQueued();
                }
            }
            finally
            {
                completion.Dispose();
            }
        }
    }

    private static int FindActiveWorkIndex(
        List<OrderedConcurrentRebalanceBatchWork> active,
        Task<OrderedConcurrentRebalanceBatchCompletion> completedTask)
    {
        for (var i = 0; i < active.Count; i++)
        {
            if (ReferenceEquals(active[i].Task, completedTask))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Completed ordered rebalance task was not found.");
    }

    private static int FindCompletionIndex(
        List<OrderedConcurrentRebalanceBatchCompletion> completed,
        long sequence)
    {
        for (var i = 0; i < completed.Count; i++)
        {
            if (completed[i].Sequence.Value == sequence)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsFailedProcessingStatus(
        RadarProcessingQueuedBatchProcessingStatus status) =>
        status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration;

    private static async ValueTask CompleteAndDiscardActiveWorkAsync(
        List<OrderedConcurrentRebalanceBatchWork> active)
    {
        foreach (var work in active)
        {
            var completion = await work.Task.ConfigureAwait(false);
            completion.Dispose();
        }

        active.Clear();
    }

    private static void DisposeCompleted(
        List<OrderedConcurrentRebalanceBatchCompletion> completed)
    {
        foreach (var completion in completed)
        {
            completion.Dispose();
        }

        completed.Clear();
    }

    private void RecordEnqueueResult(
        RadarProcessingQueuedBatchEnqueueResult result)
    {
        lock (sync)
        {
            enqueueResults.Add(result);
        }
    }

    private void RecordProcessingResult(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        lock (sync)
        {
            processingResults.Add(result);
        }
    }

    private bool IsFaulted
    {
        get
        {
            lock (sync)
            {
                return faulted;
            }
        }
    }

    private void MarkFaulted(string message)
    {
        lock (sync)
        {
            faulted = true;
            faultMessage = message;
        }

        queue.Fault(message);
    }

    private void MarkCanceledAndRecordQueued()
    {
        var canceledQueued = MarkCanceled();
        foreach (var queuedBatch in canceledQueued)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled before dequeue."));
        }
    }

    private IReadOnlyList<RadarProcessingQueuedBatch> MarkCanceled()
    {
        lock (sync)
        {
            canceled = true;
        }

        return queue.Options.ShutdownMode == RadarProcessingProviderQueueShutdownMode.CancelQueued
            ? queue.CancelQueued()
            : CloseAndReturnNoCanceledBatches();
    }

    private IReadOnlyList<RadarProcessingQueuedBatch> CloseAndReturnNoCanceledBatches()
    {
        queue.Close();
        return Array.Empty<RadarProcessingQueuedBatch>();
    }

    private void AddDrainTime(long started)
    {
        lock (sync)
        {
            totalDrainTime += Stopwatch.GetElapsedTime(started);
        }
    }

    private RadarProcessingQueuedSessionStatus GetTerminalStatus()
    {
        lock (sync)
        {
            if (canceled)
            {
                return RadarProcessingQueuedSessionStatus.Canceled;
            }

            return faulted
                ? RadarProcessingQueuedSessionStatus.Faulted
                : RadarProcessingQueuedSessionStatus.Completed;
        }
    }

    private string GetTerminalMessage()
    {
        lock (sync)
        {
            return faulted ? faultMessage : string.Empty;
        }
    }

    private RadarProcessingQueuedSessionResult CreateSessionResult(
        RadarProcessingQueuedSessionStatus status,
        string message)
    {
        RadarProcessingQueuedBatchEnqueueResult[] enqueueSnapshot;
        RadarProcessingQueuedBatchProcessingResult[] processingSnapshot;
        TimeSpan drainTime;
        lock (sync)
        {
            enqueueSnapshot = enqueueResults.ToArray();
            processingSnapshot = processingResults.ToArray();
            drainTime = totalDrainTime;
        }

        return new RadarProcessingQueuedSessionResult(
            status,
            CreateTelemetrySummary(drainTime, processingSnapshot),
            enqueueSnapshot,
            processingSnapshot,
            message,
            rebalanceSession.CurrentTopology.Version);
    }

    private RadarProcessingProviderQueueTelemetrySummary CreateTelemetrySummary(
        TimeSpan drainTime,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingSnapshot)
    {
        var queueSummary = queue.CreateTelemetrySummary();
        var completed = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        var failed = processingSnapshot.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedProcessing ||
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedValidation ||
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceledCount = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);

        return new RadarProcessingProviderQueueTelemetrySummary(
            queueSummary.OwnedSnapshotCount,
            queueSummary.OwnedSnapshotPayloadBytes,
            queueSummary.OwnedSnapshotAllocatedBytes,
            queueSummary.TotalOwnedSnapshotTime,
            queueSummary.EnqueueAttemptCount,
            queueSummary.EnqueuedBatchCount,
            queueSummary.EnqueueFullCount,
            queueSummary.EnqueueTimedOutCount,
            queueSummary.EnqueueCanceledCount,
            queueSummary.EnqueueClosedCount,
            queueSummary.EnqueueFaultedCount,
            queueSummary.TotalEnqueueWaitTime,
            queueSummary.DequeuedBatchCount,
            completed,
            failed,
            canceledCount,
            skipped,
            drainTime,
            queueSummary.QueueDepthHighWatermark,
            queueSummary.QueuedPayloadBytesHighWatermark,
            queueSummary.OwnedSnapshotPayloadValueCount,
            queueSummary.TotalProviderToProcessingLatency,
            queueSummary.RecentDetails,
            queueSummary.DroppedRecentDetailCount,
            queueSummary.OwnedSnapshotEventCount,
            queueSummary.TotalDequeueWaitTime,
            queueSummary.RetainedResourcePressure);
    }

    private static RadarProcessingAsyncRebalanceSession? CreateAsyncRebalanceSessionIfNeeded(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncRebalanceSession(rebalanceSession)
            : null;
    }

    private static bool RequiresAsyncRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport;
    }

    private sealed class OrderedConcurrentRebalanceBatchWork
    {
        public OrderedConcurrentRebalanceBatchWork(
            Task<OrderedConcurrentRebalanceBatchCompletion> task)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
        }

        public Task<OrderedConcurrentRebalanceBatchCompletion> Task { get; }
    }

    private sealed class OrderedConcurrentRebalanceBatchCompletion : IDisposable
    {
        private readonly RadarEventBatch? batch;
        private readonly IDisposable? lease;
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private OrderedConcurrentRebalanceBatchCompletion(
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch? batch,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult,
            IDisposable? lease)
        {
            Sequence = sequence;
            this.batch = batch;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
            this.lease = lease;
        }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public static OrderedConcurrentRebalanceBatchCompletion FromDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingBatchDelta delta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(delta);
            return new OrderedConcurrentRebalanceBatchCompletion(sequence, batch, delta, null, null, null, lease);
        }

        public static OrderedConcurrentRebalanceBatchCompletion FromAsyncDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingAsyncBatchDeltaResult asyncDelta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new OrderedConcurrentRebalanceBatchCompletion(
                sequence,
                batch,
                asyncDelta.Delta,
                asyncDelta,
                asyncDelta.WorkerTelemetry,
                null,
                lease);
        }

        public static OrderedConcurrentRebalanceBatchCompletion FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult processingResult,
            IDisposable? lease = null,
            bool leaseAlreadyDisposed = false)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new OrderedConcurrentRebalanceBatchCompletion(
                processingResult.Sequence,
                null,
                null,
                null,
                null,
                processingResult,
                leaseAlreadyDisposed ? null : lease);
        }

        public RadarProcessingQueuedBatchProcessingResult Commit(
            RadarProcessingRebalanceSession rebalanceSession,
            RadarProcessingAsyncRebalanceSession? asyncRebalanceSession,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rebalanceSession);
            if (processingResult is not null)
            {
                return processingResult;
            }

            if (delta is null)
            {
                throw new InvalidOperationException("Ordered concurrent rebalance completion has no delta or result.");
            }

            RecomputeStaleDeltaIfNeeded(
                rebalanceSession,
                asyncRebalanceSession,
                cancellationToken);

            var rebalanceResult = rebalanceSession.CommitProcessingDelta(
                delta,
                workerTelemetry,
                cancellationToken);
            processingResult = CreateProcessingResult(Sequence, rebalanceResult);
            return processingResult;
        }

        private void RecomputeStaleDeltaIfNeeded(
            RadarProcessingRebalanceSession rebalanceSession,
            RadarProcessingAsyncRebalanceSession? asyncRebalanceSession,
            CancellationToken cancellationToken)
        {
            if (delta is null ||
                delta.Route.TopologyVersion == rebalanceSession.CurrentTopology.Version)
            {
                return;
            }

            if (batch is null)
            {
                throw new InvalidOperationException("Ordered rebalance stale topology recompute requires the source batch.");
            }

            DisposeCurrentDelta();
            if (asyncRebalanceSession is not null)
            {
                asyncDelta = asyncRebalanceSession
                    .AsyncCoreSession
                    .ComputeDeltaAsync(batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                delta = asyncDelta.Delta;
                workerTelemetry = asyncDelta.WorkerTelemetry;
                return;
            }

            delta = rebalanceSession.Core.ComputeProcessingDelta(batch, cancellationToken);
            workerTelemetry = null;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeCurrentDelta();
            workerTelemetry = null;
            lease?.Dispose();
        }

        private void DisposeCurrentDelta()
        {
            asyncDelta?.Dispose();
            if (asyncDelta is null)
            {
                delta?.Dispose();
            }

            delta = null;
            asyncDelta = null;
        }

        private static RadarProcessingQueuedBatchProcessingResult CreateProcessingResult(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingRebalanceSessionResult rebalanceResult)
        {
            var processingResult = rebalanceResult.ProcessingResult;
            if (!processingResult.IsValid)
            {
                return RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    sequence,
                    processingResult.Validation.Message,
                    processingResult);
            }

            if (rebalanceResult.MigrationResult is { Succeeded: false } migrationResult)
            {
                return RadarProcessingQueuedBatchProcessingResult.FailedMigration(
                    sequence,
                    $"Queued rebalance migration failed with state {migrationResult.State}.",
                    rebalanceResult);
            }

            if (!rebalanceResult.Validation.IsValid)
            {
                return RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    sequence,
                    rebalanceResult.Validation.Message,
                    processingResult);
            }

            return RadarProcessingQueuedBatchProcessingResult.Succeeded(
                sequence,
                rebalanceResult);
        }
    }
}
