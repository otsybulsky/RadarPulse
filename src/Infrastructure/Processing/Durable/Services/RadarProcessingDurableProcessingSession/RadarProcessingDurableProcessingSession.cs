using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Processes durable envelope claims through a processing core and ordered commit gate.
/// </summary>
/// <remarks>
/// Durable processing separates claim/compute from ordered commit so recovered
/// completed envelopes can be replayed in provider sequence before retained
/// resources are marked released.
/// </remarks>
public sealed partial class RadarProcessingDurableProcessingSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingDurableEnvelopeQueue queue;
    private readonly RadarProcessingAsyncCoreSession? asyncCoreSession;
    private readonly bool ownsAsyncCoreSession;
    private readonly SortedDictionary<long, DurableProcessingCompletion> pendingCompletions = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private long nextCommitSequence;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a durable processing session over a queue and any required async core session.
    /// </summary>
    public RadarProcessingDurableProcessingSession(
        RadarProcessingCore core,
        RadarProcessingDurableEnvelopeQueue? queue = null)
        : this(
            core,
            queue ?? new RadarProcessingDurableEnvelopeQueue(),
            CreateAsyncCoreSessionIfNeeded(core),
            ownsAsyncCoreSession: core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
    {
    }

    /// <summary>
    /// Creates a durable processing session over explicit queue and async dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires the async core session to wrap the same
    /// core; synchronous processing rejects async dependencies.
    /// </remarks>
    public RadarProcessingDurableProcessingSession(
        RadarProcessingCore core,
        RadarProcessingDurableEnvelopeQueue queue,
        RadarProcessingAsyncCoreSession? asyncCoreSession = null,
        bool ownsAsyncCoreSession = false)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(queue);

        if (core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncCoreSession);
            if (!ReferenceEquals(core, asyncCoreSession.Core))
            {
                throw new ArgumentException(
                    "Durable async processing requires the async core session to share the supplied core.",
                    nameof(asyncCoreSession));
            }
        }
        else if (asyncCoreSession is not null)
        {
            throw new ArgumentException(
                "Durable synchronous processing must not carry an async core session.",
                nameof(asyncCoreSession));
        }

        this.core = core;
        this.queue = queue;
        this.asyncCoreSession = asyncCoreSession;
        this.ownsAsyncCoreSession = ownsAsyncCoreSession;
    }

    /// <summary>
    /// Processing core used to compute and commit durable batch work.
    /// </summary>
    public RadarProcessingCore Core => core;

    /// <summary>
    /// Durable envelope queue owned by the session caller.
    /// </summary>
    public RadarProcessingDurableEnvelopeQueue Queue => queue;

    /// <summary>
    /// Computes processing output for a claimed envelope and records the durable state.
    /// </summary>
    public async ValueTask<RadarProcessingDurableQueueOperationResult> ProcessClaimedAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(claimedEnvelope);

        var completion = await ComputeCompletionAsync(claimedEnvelope, cancellationToken)
            .ConfigureAwait(false);
        var operation = ApplyCompletionState(completion);
        if (operation.Status is RadarProcessingDurableQueueOperationStatus.Completed or
            RadarProcessingDurableQueueOperationStatus.Failed or
            RadarProcessingDurableQueueOperationStatus.Canceled)
        {
            AddCompletion(completion);
        }
        else
        {
            completion.Dispose();
        }

        return operation;
    }

    /// <summary>
    /// Commits ready completed envelopes in provider sequence order.
    /// </summary>
    /// <returns>Processing results published by this call.</returns>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CommitReady(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        List<RadarProcessingQueuedBatchProcessingResult>? published = null;
        while (true)
        {
            DurableProcessingCompletion? completion;
            lock (sync)
            {
                if (faulted || canceled || !pendingCompletions.Remove(nextCommitSequence, out completion))
                {
                    break;
                }
            }

            try
            {
                var result = completion.Commit(core, cancellationToken);
                if (result.IsSuccessful)
                {
                    queue.MarkCommitted(completion.BatchId);
                    queue.MarkReleased(completion.BatchId);
                }
                else if (result.Status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                         RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                         RadarProcessingQueuedBatchProcessingStatus.FailedMigration)
                {
                    queue.Fail(completion.BatchId, result.Message);
                    MarkFaulted(result.Message);
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    queue.Cancel(completion.BatchId, result.Message);
                    MarkCanceled();
                }

                RecordProcessingResult(result);
                published ??= [];
                published.Add(result);
                nextCommitSequence = checked(nextCommitSequence + 1);
            }
            finally
            {
                completion.Dispose();
            }
        }

        return published is null
            ? Array.Empty<RadarProcessingQueuedBatchProcessingResult>()
            : Array.AsReadOnly(published.ToArray());
    }

    /// <summary>
    /// Rebuilds pending commit work for envelopes persisted as completed.
    /// </summary>
    /// <returns>The number of completed envelopes staged for ordered commit.</returns>
    public async ValueTask<int> RecoverCompletedAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var recovered = 0;
        foreach (var snapshot in queue.CreateSnapshots())
        {
            if (snapshot.State != RadarProcessingDurableEnvelopeState.Completed)
            {
                continue;
            }

            lock (sync)
            {
                if (faulted || canceled || pendingCompletions.ContainsKey(snapshot.ProviderSequence.Value))
                {
                    continue;
                }
            }

            if (!queue.TryGetQueuedBatch(snapshot.BatchId, out var queuedBatch))
            {
                MarkFaulted($"Durable envelope '{snapshot.BatchId}' was not found during recovery.");
                break;
            }

            var completion = await ComputeCompletionAsync(
                    snapshot.BatchId,
                    queuedBatch!,
                    cancellationToken)
                .ConfigureAwait(false);
            AddCompletion(completion);
            recovered++;
        }

        return recovered;
    }

    /// <summary>
    /// Claims, processes, and commits durable envelopes until no pending work remains.
    /// </summary>
    public async ValueTask<RadarProcessingDurableProcessingSessionResult> DrainAsync(
        string workerId = "durable-local-worker",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(workerId);

        try
        {
            while (!IsFaulted && !IsCanceled)
            {
                var claim = queue.ClaimNext(workerId);
                if (claim.Status == RadarProcessingDurableQueueOperationStatus.Empty)
                {
                    break;
                }

                if (claim.Status != RadarProcessingDurableQueueOperationStatus.Claimed)
                {
                    MarkFaulted(claim.Message);
                    break;
                }

                await ProcessClaimedAsync(claim.ClaimedEnvelope!, cancellationToken)
                    .ConfigureAwait(false);
                CommitReady(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceled();
        }

        return CreateResult();
    }

    /// <summary>
    /// Applies retry policy to a failed or abandoned durable envelope.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult RetryOrPoison(
        RadarProcessingDurableBatchId batchId,
        RadarProcessingDurableRetryPolicy? retryPolicy = null,
        string retryMessage = "",
        string poisonMessage = "")
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(retryMessage);
        ArgumentNullException.ThrowIfNull(poisonMessage);

        var effectivePolicy = retryPolicy ?? RadarProcessingDurableRetryPolicy.NoRetry;
        if (!queue.TryGetSnapshot(batchId, out var snapshot))
        {
            return RadarProcessingDurableQueueOperationResult.NotFound(
                $"Durable envelope '{batchId}' was not found.");
        }

        if (effectivePolicy.CanRetry(snapshot!))
        {
            return queue.Retry(batchId, retryMessage);
        }

        if (snapshot!.State is RadarProcessingDurableEnvelopeState.Failed or
            RadarProcessingDurableEnvelopeState.Abandoned)
        {
            return queue.Poison(
                batchId,
                string.IsNullOrWhiteSpace(poisonMessage)
                    ? $"Durable envelope '{batchId}' exhausted retry policy after {snapshot.Attempt} attempt(s)."
                    : poisonMessage);
        }

        return RadarProcessingDurableQueueOperationResult.InvalidState(
            snapshot,
            $"Durable envelope '{batchId}' cannot retry from state {snapshot.State}.");
    }

    /// <summary>
    /// Cancels open durable work, releases canceled envelopes, and returns a session result.
    /// </summary>
    public RadarProcessingDurableProcessingSessionResult CancelAndCleanup(
        string message = "Durable processing was canceled.")
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(message);

        MarkCanceled();
        DisposePendingCompletions();
        queue.CancelOpen(message);
        queue.ReleaseCanceled(message);
        return CreateResult();
    }

    /// <summary>
    /// Creates a session result from current queue summary and published processing results.
    /// </summary>
    public RadarProcessingDurableProcessingSessionResult CreateResult()
    {
        RadarProcessingQueuedBatchProcessingResult[] processingSnapshot;
        bool isFaulted;
        bool isCanceled;
        string message;
        lock (sync)
        {
            processingSnapshot = processingResults.ToArray();
            isFaulted = faulted;
            isCanceled = canceled;
            message = faultMessage;
        }

        var status = isCanceled
            ? RadarProcessingQueuedSessionStatus.Canceled
            : isFaulted
                ? RadarProcessingQueuedSessionStatus.Faulted
                : RadarProcessingQueuedSessionStatus.Completed;

        return new RadarProcessingDurableProcessingSessionResult(
            status,
            queue.CreateSummary(),
            processingSnapshot,
            message);
    }

    /// <summary>
    /// Synchronously disposes pending completions and owned async core resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes pending completions and owned async core resources.
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

        foreach (var completion in pendingCompletions.Values)
        {
            completion.Dispose();
        }

        pendingCompletions.Clear();

        if (ownsAsyncCoreSession && asyncCoreSession is not null)
        {
            await asyncCoreSession.DisposeAsync().ConfigureAwait(false);
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

    private bool IsCanceled
    {
        get
        {
            lock (sync)
            {
                return canceled;
            }
        }
    }
}
