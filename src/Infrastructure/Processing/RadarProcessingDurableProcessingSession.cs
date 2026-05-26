using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingDurableProcessingSession : IDisposable, IAsyncDisposable
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

    public RadarProcessingCore Core => core;

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

    private async ValueTask<DurableProcessingCompletion> ComputeCompletionAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ComputeCompletionAsync(
                    claimedEnvelope.BatchId,
                    claimedEnvelope.QueuedBatch,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    claimedEnvelope.ProviderSequence,
                    "Durable processing batch was canceled."));
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return DurableProcessingCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    claimedEnvelope.ProviderSequence,
                    exception.Message,
                    result));
        }
        catch (Exception exception)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                claimedEnvelope.BatchId,
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    claimedEnvelope.ProviderSequence,
                    exception.Message));
        }
    }

    private async ValueTask<DurableProcessingCompletion> ComputeCompletionAsync(
        RadarProcessingDurableBatchId batchId,
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return DurableProcessingCompletion.FromProcessingResult(
                    batchId,
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Durable processing batch was canceled."));
            }

            var invalid = core.ValidateBatchForProcessing(
                queuedBatch.Batch,
                cancellationToken);
            if (invalid is not null)
            {
                return DurableProcessingCompletion.FromProcessingResult(
                    batchId,
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid));
            }

            if (asyncCoreSession is not null)
            {
                var asyncDelta = await asyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .ConfigureAwait(false);
                return DurableProcessingCompletion.FromAsyncDelta(
                    batchId,
                    queuedBatch.Sequence,
                    asyncDelta);
            }

            var delta = core.ComputeProcessingDelta(
                queuedBatch.Batch,
                cancellationToken);
            return DurableProcessingCompletion.FromDelta(
                batchId,
                queuedBatch.Sequence,
                delta);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                batchId,
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Durable processing batch was canceled."));
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return DurableProcessingCompletion.FromProcessingResult(
                batchId,
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result));
        }
        catch (Exception exception)
        {
            return DurableProcessingCompletion.FromProcessingResult(
                batchId,
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message));
        }
    }

    private RadarProcessingDurableQueueOperationResult ApplyCompletionState(
        DurableProcessingCompletion completion)
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
        DurableProcessingCompletion completion)
    {
        lock (sync)
        {
            if (!pendingCompletions.TryAdd(completion.Sequence.Value, completion))
            {
                throw new InvalidOperationException(
                    $"Durable processing sequence {completion.Sequence.Value} has already completed.");
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

    private sealed class DurableProcessingCompletion : IDisposable
    {
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private DurableProcessingCompletion(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult)
        {
            BatchId = batchId;
            Sequence = sequence;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
        }

        public RadarProcessingDurableBatchId BatchId { get; }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public RadarProcessingQueuedBatchProcessingResult? ProcessingResult => processingResult;

        public static DurableProcessingCompletion FromDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta delta)
        {
            ArgumentNullException.ThrowIfNull(delta);
            return new DurableProcessingCompletion(batchId, sequence, delta, null, null, null);
        }

        public static DurableProcessingCompletion FromAsyncDelta(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingAsyncBatchDeltaResult asyncDelta)
        {
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new DurableProcessingCompletion(
                batchId,
                sequence,
                asyncDelta.Delta,
                asyncDelta,
                asyncDelta.WorkerTelemetry,
                null);
        }

        public static DurableProcessingCompletion FromProcessingResult(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatchProcessingResult processingResult)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new DurableProcessingCompletion(
                batchId,
                processingResult.Sequence,
                null,
                null,
                null,
                processingResult);
        }

        public RadarProcessingQueuedBatchProcessingResult Commit(
            RadarProcessingCore core,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(core);
            if (processingResult is not null)
            {
                return processingResult;
            }

            if (delta is null)
            {
                throw new InvalidOperationException("Durable processing completion has no delta or result.");
            }

            var result = core.CommitProcessingDelta(
                delta,
                workerTelemetry,
                cancellationToken);
            processingResult = result.IsValid
                ? RadarProcessingQueuedBatchProcessingResult.Succeeded(Sequence, result)
                : RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    Sequence,
                    result.Validation.Message,
                    result);
            return processingResult;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            asyncDelta?.Dispose();
            if (asyncDelta is null)
            {
                delta?.Dispose();
            }

            delta = null;
            asyncDelta = null;
            workerTelemetry = null;
        }
    }

    private static RadarProcessingAsyncCoreSession? CreateAsyncCoreSessionIfNeeded(
        RadarProcessingCore core)
    {
        ArgumentNullException.ThrowIfNull(core);
        return core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncCoreSession(core)
            : null;
    }
}
