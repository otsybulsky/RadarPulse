using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingDurableRebalanceSession : IDisposable, IAsyncDisposable
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

    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    public RadarProcessingCore Core => rebalanceSession.Core;

    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    public RadarProcessingDurableEnvelopeQueue Queue => queue;

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

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

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

    private async ValueTask<DurableRebalanceCompletion> ComputeCompletionAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return DurableRebalanceCompletion.FromProcessingResult(
                    claimedEnvelope.BatchId,
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        claimedEnvelope.ProviderSequence,
                        "Durable rebalance batch was canceled."));
            }

            var invalid = rebalanceSession.Core.ValidateBatchForProcessing(
                claimedEnvelope.QueuedBatch.Batch,
                cancellationToken);
            if (invalid is not null)
            {
                return DurableRebalanceCompletion.FromProcessingResult(
                    claimedEnvelope.BatchId,
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        claimedEnvelope.ProviderSequence,
                        invalid.Validation.Message,
                        invalid));
            }

            if (asyncRebalanceSession is not null)
            {
                var asyncDelta = await asyncRebalanceSession
                    .AsyncCoreSession
                    .ComputeDeltaAsync(claimedEnvelope.QueuedBatch.Batch, cancellationToken)
                    .ConfigureAwait(false);
                return DurableRebalanceCompletion.FromAsyncDelta(
                    claimedEnvelope.BatchId,
                    claimedEnvelope.ProviderSequence,
                    claimedEnvelope.QueuedBatch.Batch,
                    asyncDelta);
            }

            var delta = rebalanceSession.Core.ComputeProcessingDelta(
                claimedEnvelope.QueuedBatch.Batch,
                cancellationToken);
            return DurableRebalanceCompletion.FromDelta(
                claimedEnvelope.BatchId,
                claimedEnvelope.ProviderSequence,
                claimedEnvelope.QueuedBatch.Batch,
                delta);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DurableRebalanceCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    claimedEnvelope.ProviderSequence,
                    "Durable rebalance batch was canceled."));
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = rebalanceSession.Core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return DurableRebalanceCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    claimedEnvelope.ProviderSequence,
                    exception.Message,
                    result));
        }
        catch (Exception exception)
        {
            return DurableRebalanceCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    claimedEnvelope.ProviderSequence,
                    exception.Message));
        }
    }

    private RadarProcessingDurableQueueOperationResult ApplyCompletionState(
        DurableRebalanceCompletion completion)
    {
        var result = completion.ProcessingResult;
        if (result is null)
        {
            return queue.Complete(completion.BatchId);
        }

        return result.Status switch
        {
            RadarProcessingQueuedBatchProcessingStatus.Canceled =>
                queue.Cancel(completion.BatchId, result.Message),
            RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
                RadarProcessingQueuedBatchProcessingStatus.FailedMigration =>
                queue.Fail(completion.BatchId, result.Message),
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault =>
                queue.Cancel(completion.BatchId, result.Message),
            _ => queue.Complete(completion.BatchId)
        };
    }

    private void AddCompletion(
        DurableRebalanceCompletion completion)
    {
        lock (sync)
        {
            if (!pendingCompletions.TryAdd(completion.Sequence.Value, completion))
            {
                throw new InvalidOperationException(
                    $"Durable rebalance sequence {completion.Sequence.Value} has already completed.");
            }
        }
    }

    private void DisposePendingCompletions()
    {
        lock (sync)
        {
            foreach (var completion in pendingCompletions.Values)
            {
                completion.Dispose();
            }

            pendingCompletions.Clear();
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

    private void MarkFaulted(
        string message)
    {
        lock (sync)
        {
            faulted = true;
            faultMessage = message;
        }
    }

    private void MarkCanceled()
    {
        lock (sync)
        {
            canceled = true;
        }
    }

    private sealed class DurableRebalanceCompletion : IDisposable
    {
        private readonly RadarEventBatch? batch;
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private DurableRebalanceCompletion(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch? batch,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult)
        {
            BatchId = batchId;
            Sequence = sequence;
            this.batch = batch;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
        }

        public RadarProcessingDurableBatchId BatchId { get; }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public RadarProcessingQueuedBatchProcessingResult? ProcessingResult => processingResult;

        public static DurableRebalanceCompletion FromDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingBatchDelta delta)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(delta);
            return new DurableRebalanceCompletion(batchId, sequence, batch, delta, null, null, null);
        }

        public static DurableRebalanceCompletion FromAsyncDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarEventBatch batch,
            RadarProcessingAsyncBatchDeltaResult asyncDelta)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new DurableRebalanceCompletion(
                batchId,
                sequence,
                batch,
                asyncDelta.Delta,
                asyncDelta,
                asyncDelta.WorkerTelemetry,
                null);
        }

        public static DurableRebalanceCompletion FromProcessingResult(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchProcessingResult processingResult)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new DurableRebalanceCompletion(
                batchId,
                processingResult.Sequence,
                null,
                null,
                null,
                null,
                processingResult);
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
                throw new InvalidOperationException("Durable rebalance completion has no delta or result.");
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

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisposeCurrentDelta();
            workerTelemetry = null;
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
                throw new InvalidOperationException("Durable rebalance stale topology recompute requires the source batch.");
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
                    $"Durable rebalance migration failed with state {migrationResult.State}.",
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

    private static RadarProcessingAsyncRebalanceSession? CreateAsyncRebalanceSessionIfNeeded(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncRebalanceSession(rebalanceSession)
            : null;
    }
}
