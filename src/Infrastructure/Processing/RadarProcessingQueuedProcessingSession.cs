using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly RadarProcessingAsyncCoreSession? asyncCoreSession;
    private readonly bool ownsQueue;
    private readonly bool ownsAsyncCoreSession;
    private readonly Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private TimeSpan totalDrainTime;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    public RadarProcessingQueuedProcessingSession(
        RadarProcessingCore core,
        RadarProcessingProviderQueueOptions? queueOptions = null,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
        : this(
            core,
            new RadarProcessingOwnedBatchQueue(queueOptions),
            CreateAsyncCoreSessionIfNeeded(core),
            ownsQueue: true,
            ownsAsyncCoreSession: core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport,
            consumerResourceLeaseFactory: consumerResourceLeaseFactory)
    {
    }

    public RadarProcessingQueuedProcessingSession(
        RadarProcessingCore core,
        RadarProcessingOwnedBatchQueue queue,
        RadarProcessingAsyncCoreSession? asyncCoreSession = null,
        bool ownsQueue = false,
        bool ownsAsyncCoreSession = false,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(queue);

        if (core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncCoreSession);
            if (!ReferenceEquals(core, asyncCoreSession.Core))
            {
                throw new ArgumentException(
                    "Queued async processing requires the async core session to share the supplied core.",
                    nameof(asyncCoreSession));
            }
        }
        else if (asyncCoreSession is not null)
        {
            throw new ArgumentException(
                "Queued synchronous processing must not carry an async core session.",
                nameof(asyncCoreSession));
        }

        this.core = core;
        this.queue = queue;
        this.asyncCoreSession = asyncCoreSession;
        this.ownsQueue = ownsQueue;
        this.ownsAsyncCoreSession = ownsAsyncCoreSession;
        this.consumerResourceLeaseFactory = consumerResourceLeaseFactory;
    }

    public RadarProcessingCore Core => core;

    public RadarProcessingOwnedBatchQueue Queue => queue;

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

    public void CompleteAdding() => queue.Close();

    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        MarkFaulted(message);
    }

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
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");

                    case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Disposed, "Queued processing queue was disposed.");

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
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");
        }
    }

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
        var active = new List<OrderedConcurrentBatchWork>(effectiveOptions.ActiveBatchCapacity);
        var completed = new List<OrderedConcurrentBatchCompletion>(effectiveOptions.ActiveBatchCapacity);
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
                                "Queued processing queue was disposed.");

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
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");
        }
        finally
        {
            DisposeCompleted(completed);
        }
    }

    public async ValueTask<RadarProcessingQueuedSessionResult> DrainOrderedHandlerDeltaMergeAsync(
        RadarProcessingOrderedConcurrencyOptions? orderedOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var effectiveOptions = orderedOptions ?? RadarProcessingOrderedConcurrencyOptions.Default;
        if (effectiveOptions.IsSequential)
        {
            return await DrainAsync(cancellationToken).ConfigureAwait(false);
        }

        var handlerMergeCoordinators = CreateHandlerDeltaMergeCoordinators(core);
        var started = Stopwatch.GetTimestamp();
        using var activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var active = new List<OrderedConcurrentBatchWork>(effectiveOptions.ActiveBatchCapacity);
        var completed = new List<OrderedConcurrentBatchCompletion>(effectiveOptions.ActiveBatchCapacity);
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
                                active.Add(StartOrderedConcurrentHandlerDeltaBatch(queuedBatch, activeCancellation.Token));
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
                                "Queued processing queue was disposed.");

                        default:
                            RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                            throw new ArgumentOutOfRangeException(nameof(dequeue));
                    }
                }

                PublishReadyHandlerDeltaMergeCompletions(
                    completed,
                    ref nextPublishSequence,
                    activeCancellation,
                    handlerMergeCoordinators);
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
                PublishReadyHandlerDeltaMergeCompletions(
                    completed,
                    ref nextPublishSequence,
                    activeCancellation,
                    handlerMergeCoordinators);
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
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");
        }
        finally
        {
            DisposeCompleted(completed);
        }
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

        if (ownsAsyncCoreSession && asyncCoreSession is not null)
        {
            await asyncCoreSession.DisposeAsync().ConfigureAwait(false);
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

            var processingResult = asyncCoreSession is null
                ? core.Process(queuedBatch.Batch, cancellationToken)
                : await asyncCoreSession.ProcessAsync(queuedBatch.Batch, cancellationToken).ConfigureAwait(false);

            if (processingResult.IsValid)
            {
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Succeeded(
                        queuedBatch.Sequence,
                        processingResult));
                return;
            }

            var message = processingResult.Validation.Message;
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    message,
                    processingResult));
            MarkFaulted(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."));
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

    private OrderedConcurrentBatchWork StartOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentBatchWork(task);
    }

    private OrderedConcurrentBatchWork StartOrderedConcurrentHandlerDeltaBatch(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        var task = Task.Run(
            () => ComputeOrderedConcurrentHandlerDeltaBatch(queuedBatch, lease, cancellationToken),
            CancellationToken.None);
        return new OrderedConcurrentBatchWork(task);
    }

    private OrderedConcurrentBatchCompletion CreateSkippedAfterFaultCompletion(
        RadarProcessingQueuedBatch queuedBatch)
    {
        using var lease = consumerResourceLeaseFactory?.Invoke(queuedBatch.Sequence);
        return OrderedConcurrentBatchCompletion.FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                queuedBatch.Sequence,
                faultMessage));
    }

    private OrderedConcurrentBatchCompletion ComputeOrderedConcurrentBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued processing batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            if (asyncCoreSession is not null)
            {
                var asyncDelta = asyncCoreSession
                    .ComputeDeltaAsync(queuedBatch.Batch, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                return OrderedConcurrentBatchCompletion.FromAsyncDelta(queuedBatch.Sequence, asyncDelta, lease);
            }

            var delta = core.ComputeProcessingDelta(queuedBatch.Batch, cancellationToken);
            return OrderedConcurrentBatchCompletion.FromDelta(queuedBatch.Sequence, delta, lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private OrderedConcurrentBatchCompletion ComputeOrderedConcurrentHandlerDeltaBatch(
        RadarProcessingQueuedBatch queuedBatch,
        IDisposable? lease,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                lease?.Dispose();
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.Canceled(
                        queuedBatch.Sequence,
                        "Queued processing batch was canceled."),
                    leaseAlreadyDisposed: true);
            }

            var invalid = core.ValidateBatchForProcessing(queuedBatch.Batch, cancellationToken);
            if (invalid is not null)
            {
                return OrderedConcurrentBatchCompletion.FromProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        invalid.Validation.Message,
                        invalid),
                    lease);
            }

            var delta = core.ComputeProcessingDeltaForHandlerDeltaMerge(queuedBatch.Batch, cancellationToken);
            var handlerDeltas = CreateHandlerDeltas(queuedBatch, cancellationToken);
            return OrderedConcurrentBatchCompletion.FromHandlerDeltas(
                queuedBatch.Sequence,
                delta,
                handlerDeltas,
                lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued processing batch was canceled."),
                lease);
        }
        catch (RadarProcessingBatchDeltaValidationException exception)
        {
            var result = core.CreateInvalidProcessingResult(
                exception.Error,
                exception.SourceId,
                exception.EventIndex,
                exception.Message);
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                    queuedBatch.Sequence,
                    exception.Message,
                    result),
                lease);
        }
        catch (Exception exception)
        {
            return OrderedConcurrentBatchCompletion.FromProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message),
                lease);
        }
    }

    private void PublishReadyOrderedCompletions(
        List<OrderedConcurrentBatchCompletion> completed,
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
                    : completion.Commit(core, activeCancellation.Token);
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

    private void PublishReadyHandlerDeltaMergeCompletions(
        List<OrderedConcurrentBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation,
        IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator> handlerMergeCoordinators)
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
                    : completion.Commit(core, activeCancellation.Token, handlerMergeCoordinators);
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

    private IReadOnlyList<RadarProcessingHandlerDelta> CreateHandlerDeltas(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        var contract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        if (!contract.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            throw new NotSupportedException(
                "Ordered handler delta/merge requires all handlers to be mergeable.");
        }

        var tempCore = new RadarProcessingCore(
            core.SourceUniverse,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.Sequential,
                partitionCount: 1,
                shardCount: 1,
                enableValidation: core.Options.EnableValidation,
                handlers: core.Options.Handlers));
        var processingResult = tempCore.Process(queuedBatch.Batch, cancellationToken);
        if (!processingResult.IsValid)
        {
            throw new InvalidOperationException(processingResult.Validation.Message);
        }

        var snapshots = tempCore.CreateSourceHandlerSnapshots();
        var result = new RadarProcessingHandlerDelta[contract.Handlers.Count];
        for (var handlerIndex = 0; handlerIndex < contract.Handlers.Count; handlerIndex++)
        {
            var descriptor = contract.Handlers[handlerIndex];
            if (core.Options.Handlers[descriptor.HandlerIndex] is not IRadarProcessingHandlerDeltaMerger merger)
            {
                throw new NotSupportedException(
                    $"Mergeable handler '{descriptor.Name}' must implement the handler delta merger contract.");
            }

            if (!string.Equals(merger.HandlerName, descriptor.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Mergeable handler '{descriptor.Name}' merger name does not match its descriptor.");
            }

            result[handlerIndex] = RadarProcessingHandlerDelta.Create(
                descriptor.Name,
                merger.HandlerContractVersion,
                queuedBatch.Sequence,
                durableBatchId: null,
                queuedBatch.StreamEventCount,
                core.SourceCount,
                queuedBatch.PayloadValueCount,
                queuedBatch.RawValueChecksum,
                CreateHandlerDeltaValues(descriptor, snapshots));
        }

        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<RadarProcessingHandlerDeltaValue> CreateHandlerDeltaValues(
        RadarProcessingHandlerOutputDescriptor descriptor,
        IReadOnlyList<RadarSourceProcessingHandlerSnapshot> snapshots)
    {
        var result = new List<RadarProcessingHandlerDeltaValue>(
            checked(descriptor.Fields.Count * snapshots.Count));
        foreach (var snapshot in snapshots)
        {
            foreach (var field in descriptor.Fields)
            {
                if (!snapshot.TryGetValue(field.Name, out var value))
                {
                    throw new InvalidOperationException(
                        $"Handler snapshot is missing field '{field.Name}'.");
                }

                result.Add(value.Type switch
                {
                    RadarSourceProcessingSnapshotFieldType.Int64 =>
                        RadarProcessingHandlerDeltaValue.ForInt64(
                            snapshot.SourceId,
                            field.Name,
                            value.Int64Value),
                    RadarSourceProcessingSnapshotFieldType.Double =>
                        RadarProcessingHandlerDeltaValue.ForDouble(
                            snapshot.SourceId,
                            field.Name,
                            value.DoubleValue),
                    _ => throw new InvalidOperationException("Unsupported handler snapshot field type.")
                });
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator> CreateHandlerDeltaMergeCoordinators(
        RadarProcessingCore core)
    {
        var contract = RadarProcessingHandlerOutputContract.FromOptions(core.Options);
        if (!contract.AllowsOrderedConcurrentHandlerDeltaMerge)
        {
            throw new NotSupportedException(
                "Ordered handler delta/merge requires a mergeable handler output contract.");
        }

        var result = new Dictionary<string, RadarProcessingHandlerDeltaMergeCoordinator>(StringComparer.Ordinal);
        foreach (var descriptor in contract.Handlers)
        {
            if (core.Options.Handlers[descriptor.HandlerIndex] is not IRadarProcessingHandlerDeltaMerger merger)
            {
                throw new NotSupportedException(
                    $"Mergeable handler '{descriptor.Name}' must implement the handler delta merger contract.");
            }

            if (!string.Equals(merger.HandlerName, descriptor.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Mergeable handler '{descriptor.Name}' merger name does not match its descriptor.");
            }

            result.Add(
                descriptor.Name,
                new RadarProcessingHandlerDeltaMergeCoordinator(merger));
        }

        return result;
    }

    private static int FindActiveWorkIndex(
        List<OrderedConcurrentBatchWork> active,
        Task<OrderedConcurrentBatchCompletion> completedTask)
    {
        for (var i = 0; i < active.Count; i++)
        {
            if (ReferenceEquals(active[i].Task, completedTask))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Completed ordered processing task was not found.");
    }

    private static int FindCompletionIndex(
        List<OrderedConcurrentBatchCompletion> completed,
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
        List<OrderedConcurrentBatchWork> active)
    {
        foreach (var work in active)
        {
            var completion = await work.Task.ConfigureAwait(false);
            completion.Dispose();
        }

        active.Clear();
    }

    private static void DisposeCompleted(
        List<OrderedConcurrentBatchCompletion> completed)
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
                    "Queued processing batch was canceled before dequeue."));
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
            message);
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

    private sealed class OrderedConcurrentBatchWork
    {
        public OrderedConcurrentBatchWork(
            Task<OrderedConcurrentBatchCompletion> task)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
        }

        public Task<OrderedConcurrentBatchCompletion> Task { get; }
    }

    private sealed class OrderedConcurrentBatchCompletion : IDisposable
    {
        private readonly IDisposable? lease;
        private RadarProcessingBatchDelta? delta;
        private RadarProcessingAsyncBatchDeltaResult? asyncDelta;
        private IReadOnlyList<RadarProcessingHandlerDelta>? handlerDeltas;
        private RadarProcessingWorkerTelemetrySummary? workerTelemetry;
        private RadarProcessingQueuedBatchProcessingResult? processingResult;
        private bool disposed;

        private OrderedConcurrentBatchCompletion(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta? delta,
            RadarProcessingAsyncBatchDeltaResult? asyncDelta,
            IReadOnlyList<RadarProcessingHandlerDelta>? handlerDeltas,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            RadarProcessingQueuedBatchProcessingResult? processingResult,
            IDisposable? lease)
        {
            Sequence = sequence;
            this.delta = delta;
            this.asyncDelta = asyncDelta;
            this.handlerDeltas = handlerDeltas;
            this.workerTelemetry = workerTelemetry;
            this.processingResult = processingResult;
            this.lease = lease;
        }

        public RadarProcessingQueuedBatchSequence Sequence { get; }

        public static OrderedConcurrentBatchCompletion FromDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta delta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(delta);
            return new OrderedConcurrentBatchCompletion(sequence, delta, null, null, null, null, lease);
        }

        public static OrderedConcurrentBatchCompletion FromAsyncDelta(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingAsyncBatchDeltaResult asyncDelta,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(asyncDelta);
            return new OrderedConcurrentBatchCompletion(
                sequence,
                asyncDelta.Delta,
                asyncDelta,
                null,
                asyncDelta.WorkerTelemetry,
                null,
                lease);
        }

        public static OrderedConcurrentBatchCompletion FromHandlerDeltas(
            RadarProcessingQueuedBatchSequence sequence,
            RadarProcessingBatchDelta delta,
            IReadOnlyList<RadarProcessingHandlerDelta> handlerDeltas,
            IDisposable? lease)
        {
            ArgumentNullException.ThrowIfNull(delta);
            ArgumentNullException.ThrowIfNull(handlerDeltas);
            return new OrderedConcurrentBatchCompletion(
                sequence,
                delta,
                null,
                handlerDeltas,
                null,
                null,
                lease);
        }

        public static OrderedConcurrentBatchCompletion FromProcessingResult(
            RadarProcessingQueuedBatchProcessingResult processingResult,
            IDisposable? lease = null,
            bool leaseAlreadyDisposed = false)
        {
            ArgumentNullException.ThrowIfNull(processingResult);
            return new OrderedConcurrentBatchCompletion(
                processingResult.Sequence,
                null,
                null,
                null,
                null,
                processingResult,
                leaseAlreadyDisposed ? null : lease);
        }

        public RadarProcessingQueuedBatchProcessingResult Commit(
            RadarProcessingCore core,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator>? handlerMergeCoordinators = null)
        {
            ArgumentNullException.ThrowIfNull(core);
            if (processingResult is not null)
            {
                return processingResult;
            }

            if (delta is null)
            {
                throw new InvalidOperationException("Ordered concurrent completion has no delta or result.");
            }

            if (handlerDeltas is not null)
            {
                ArgumentNullException.ThrowIfNull(handlerMergeCoordinators);
                var invalid = core.ValidateProcessingDeltaForCommit(delta, cancellationToken);
                if (invalid is not null)
                {
                    processingResult = RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        Sequence,
                        invalid.Validation.Message,
                        invalid);
                    return processingResult;
                }

                foreach (var handlerDelta in handlerDeltas)
                {
                    if (!handlerMergeCoordinators.TryGetValue(
                            handlerDelta.HandlerName,
                            out var coordinator))
                    {
                        processingResult = RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                            Sequence,
                            $"No merge coordinator exists for handler '{handlerDelta.HandlerName}'.");
                        return processingResult;
                    }

                    var mergeResult = coordinator.Complete(handlerDelta);
                    if (mergeResult.IsRejected || mergeResult.IsBlocked)
                    {
                        processingResult = RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                            Sequence,
                            mergeResult.Message);
                        return processingResult;
                    }
                }

                var mergedHandlerValues = handlerMergeCoordinators.Values
                    .SelectMany(static coordinator => coordinator.CreateSummary().MergedValues)
                    .ToArray();
                var handlerResult = core.CommitValidatedProcessingDeltaWithMergedHandlerValues(
                    delta,
                    mergedHandlerValues,
                    workerTelemetry,
                    cancellationToken);
                processingResult = handlerResult.IsValid
                    ? RadarProcessingQueuedBatchProcessingResult.Succeeded(Sequence, handlerResult)
                    : RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        Sequence,
                        handlerResult.Validation.Message,
                        handlerResult);
                return processingResult;
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

            asyncDelta = null;
            delta = null;
            handlerDeltas = null;
            workerTelemetry = null;
            lease?.Dispose();
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
