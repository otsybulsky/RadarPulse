using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Processes durable envelope claims through a rebalance session and ordered commit gate.
/// </summary>
/// <remarks>
/// Durable rebalance keeps provider-sequence ordering across recoverable work
/// while allowing rebalance processing and topology changes to be committed only
/// when earlier completed envelopes have published.
/// </remarks>
public sealed partial class RadarProcessingDurableRebalanceSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingDurableEnvelopeQueue queue;
    private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
    private readonly bool ownsAsyncRebalanceSession;
    private readonly SortedDictionary<long, DurableRebalanceCompletion> pendingCompletions = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private long nextCommitSequence;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a durable rebalance session over a queue and any required async rebalance session.
    /// </summary>
    public RadarProcessingDurableRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingDurableEnvelopeQueue? queue = null)
        : this(
            rebalanceSession,
            queue ?? new RadarProcessingDurableEnvelopeQueue(),
            CreateAsyncRebalanceSessionIfNeeded(rebalanceSession),
            ownsAsyncRebalanceSession: rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
    {
    }

    /// <summary>
    /// Creates a durable rebalance session over explicit queue and async dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires the async rebalance session to wrap the
    /// same rebalance session; synchronous processing rejects async dependencies.
    /// </remarks>
    public RadarProcessingDurableRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingDurableEnvelopeQueue queue,
        RadarProcessingAsyncRebalanceSession? asyncRebalanceSession = null,
        bool ownsAsyncRebalanceSession = false)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        ArgumentNullException.ThrowIfNull(queue);

        if (rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncRebalanceSession);
            if (!ReferenceEquals(rebalanceSession, asyncRebalanceSession.RebalanceSession))
            {
                throw new ArgumentException(
                    "Durable async rebalance requires the async rebalance session to wrap the supplied rebalance session.",
                    nameof(asyncRebalanceSession));
            }
        }
        else if (asyncRebalanceSession is not null)
        {
            throw new ArgumentException(
                "Durable synchronous rebalance must not carry an async rebalance session.",
                nameof(asyncRebalanceSession));
        }

        this.rebalanceSession = rebalanceSession;
        this.queue = queue;
        this.asyncRebalanceSession = asyncRebalanceSession;
        this.ownsAsyncRebalanceSession = ownsAsyncRebalanceSession;
    }

    /// <summary>
    /// Rebalance session used to process and commit durable work.
    /// </summary>
    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    /// <summary>
    /// Processing core owned by the rebalance session.
    /// </summary>
    public RadarProcessingCore Core => rebalanceSession.Core;

    /// <summary>
    /// Current topology after committed durable rebalance work.
    /// </summary>
    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    /// <summary>
    /// Durable envelope queue owned by the session caller.
    /// </summary>
    public RadarProcessingDurableEnvelopeQueue Queue => queue;

    /// <summary>
    /// Computes rebalance output for a claimed envelope and records the durable state.
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
    /// Commits ready completed rebalance envelopes in provider sequence order.
    /// </summary>
    /// <returns>Processing results published by this call.</returns>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> CommitReady(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        List<RadarProcessingQueuedBatchProcessingResult>? published = null;
        while (true)
        {
            DurableRebalanceCompletion? completion;
            lock (sync)
            {
                if (faulted || canceled || !pendingCompletions.Remove(nextCommitSequence, out completion))
                {
                    break;
                }
            }

            try
            {
                var result = completion.Commit(
                    rebalanceSession,
                    asyncRebalanceSession,
                    cancellationToken);
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
    /// Claims, processes, and commits durable rebalance envelopes until no pending work remains.
    /// </summary>
    public async ValueTask<RadarProcessingDurableRebalanceSessionResult> DrainAsync(
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
    /// Applies retry policy to a failed or abandoned durable rebalance envelope.
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
    /// Cancels open durable rebalance work, releases canceled envelopes, and returns a result.
    /// </summary>
    public RadarProcessingDurableRebalanceSessionResult CancelAndCleanup(
        string message = "Durable rebalance was canceled.")
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
    /// Creates a session result from current queue summary, processing results, and topology version.
    /// </summary>
    public RadarProcessingDurableRebalanceSessionResult CreateResult()
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

        return new RadarProcessingDurableRebalanceSessionResult(
            status,
            queue.CreateSummary(),
            processingSnapshot,
            message,
            rebalanceSession.CurrentTopology.Version);
    }

    /// <summary>
    /// Synchronously disposes pending completions and owned async rebalance resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes pending completions and owned async rebalance resources.
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

        DisposePendingCompletions();
        if (ownsAsyncRebalanceSession && asyncRebalanceSession is not null)
        {
            await asyncRebalanceSession.DisposeAsync().ConfigureAwait(false);
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
