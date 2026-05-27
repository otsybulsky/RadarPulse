using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Lifecycle-managed group of async workers used by shard transport dispatch.
/// </summary>
/// <remarks>
/// The group owns worker mailboxes, validates dispatch lifecycle state, enforces
/// single in-flight dispatch by default, maps enqueue/timeout/cancellation
/// outcomes to result contracts, and exposes drain evidence for telemetry.
/// </remarks>
public sealed class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private readonly object lifecycleSync = new();
    private readonly RadarProcessingAsyncWorker[] workers;
    private readonly RadarProcessingWorkerGroupLifecycle lifecycle;
    private readonly CancellationTokenSource workerCancellation = new();
    private int inFlight;
    private int disposeRequested;
    private int cancellationDisposed;

    /// <summary>
    /// Creates a worker group with the supplied async execution options.
    /// </summary>
    public RadarProcessingAsyncWorkerGroup(
        RadarProcessingAsyncWorkerGroupOptions? options = null)
    {
        Options = options ?? RadarProcessingAsyncWorkerGroupOptions.Default;
        lifecycle = new RadarProcessingWorkerGroupLifecycle(Options.Execution);
        workers = CreateWorkers(Options);
    }

    /// <summary>
    /// Effective worker count, queue capacity, and timeout settings.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupOptions Options { get; }

    /// <summary>
    /// Current lifecycle status for dispatch and shutdown decisions.
    /// </summary>
    public RadarProcessingWorkerGroupStatus Status
    {
        get
        {
            lock (lifecycleSync)
            {
                return lifecycle.Status;
            }
        }
    }

    /// <summary>
    /// Number of accepted work items waiting in worker mailboxes.
    /// </summary>
    public int PendingWorkItemCount => workers.Sum(static worker => worker.PendingCount);

    /// <summary>
    /// Number of work items currently executing across workers.
    /// </summary>
    public int RunningWorkItemCount => workers.Sum(static worker => worker.RunningCount);

    /// <summary>
    /// Number of accepted work items that are either pending or running.
    /// </summary>
    public int OutstandingWorkItemCount => PendingWorkItemCount + RunningWorkItemCount;

    /// <summary>
    /// Starts worker loops if the lifecycle allows dispatch.
    /// </summary>
    public RadarProcessingWorkerLifecycleResult Start()
    {
        lock (lifecycleSync)
        {
            var result = lifecycle.Start();
            if (!result.IsSuccess)
            {
                return result;
            }

            foreach (var worker in workers)
            {
                worker.Start(workerCancellation.Token, MarkFaulted);
            }

            return result;
        }
    }

    /// <summary>
    /// Stops accepting new dispatches while allowing workers to drain accepted requests.
    /// </summary>
    public RadarProcessingWorkerLifecycleResult StopAccepting()
    {
        RadarProcessingWorkerLifecycleResult result;
        lock (lifecycleSync)
        {
            result = lifecycle.StopAccepting();
        }

        if (result.IsSuccess)
        {
            CloseWorkers();
        }

        return result;
    }

    /// <summary>
    /// Stops accepting work and waits for worker loops to finish.
    /// </summary>
    public async ValueTask<RadarProcessingWorkerLifecycleResult> StopAsync()
    {
        RadarProcessingWorkerLifecycleResult result;
        lock (lifecycleSync)
        {
            result = lifecycle.Stop();
        }

        if (result.IsSuccess)
        {
            CloseWorkers();
            await AwaitWorkersAsync().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Dispatches a complete batch scope to worker mailboxes and waits for the batch barrier.
    /// </summary>
    /// <remarks>
    /// The method validates work item coverage before enqueue. When concurrent
    /// dispatch is not explicitly allowed, only one dispatch may be active at a
    /// time so mailbox capacity and completion accounting stay deterministic.
    /// </remarks>
    public async ValueTask<RadarProcessingAsyncWorkerGroupResult> DispatchAsync(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        RadarProcessingAsyncWorkExecutor executor,
        CancellationToken cancellationToken = default,
        bool allowConcurrentDispatch = false)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(workItems);
        ArgumentNullException.ThrowIfNull(executor);

        ValidateWorkItems(scope, workItems);

        RadarProcessingWorkerGroupStatus status;
        lock (lifecycleSync)
        {
            var allowed = lifecycle.ValidateDispatchAllowed();
            status = allowed.Status;
            if (!allowed.IsSuccess)
            {
                return RadarProcessingAsyncWorkerGroupResult.Rejected(
                    status,
                    MapLifecycleError(allowed.Error),
                    drainResult: CaptureDrainResult());
            }
        }

        if (scope.IsClosed)
        {
            return RadarProcessingAsyncWorkerGroupResult.Rejected(
                status,
                RadarProcessingAsyncWorkerGroupError.ScopeClosed,
                drainResult: CaptureDrainResult());
        }

        if (cancellationToken.IsCancellationRequested)
        {
            var canceledResult = CancelBeforeDispatch(scope, workItems);
            return RadarProcessingAsyncWorkerGroupResult.Completed(
                status,
                canceledResult,
                CaptureDrainResult(
                    batchResult: canceledResult,
                    cancellationRequested: true),
                cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch);
        }

        var ownsInFlight = false;
        if (!allowConcurrentDispatch)
        {
            if (Interlocked.CompareExchange(ref inFlight, 1, 0) != 0)
            {
                return RadarProcessingAsyncWorkerGroupResult.Rejected(
                    Status,
                    RadarProcessingAsyncWorkerGroupError.AlreadyInFlight,
                    drainResult: CaptureDrainResult());
            }

            ownsInFlight = true;
        }

        try
        {
            if (!CanFitCurrentMailboxCapacity(workItems))
            {
                return RadarProcessingAsyncWorkerGroupResult.Rejected(
                    Status,
                    RadarProcessingAsyncWorkerGroupError.EnqueueRejected,
                    drainResult: CaptureDrainResult());
            }

            var batchState = new RadarProcessingAsyncWorkerGroupBatchState(scope, workItems.Count);
            var enqueueError = RadarProcessingAsyncWorkerGroupError.None;
            var enqueuedTimestamp = Stopwatch.GetTimestamp();
            var barrierStartedTimestamp = Stopwatch.GetTimestamp();
            var acceptedWorkItemCount = 0;
            var timeoutResult = RadarProcessingAsyncTimeoutResult.None;
            RadarProcessingWorkerGroupHealthTransition? healthTransition = null;
            using var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var externalCancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.UnsafeRegister(
                    static state => ((RadarProcessingAsyncWorkerGroupBatchState)state!).MarkExternalCancellationRequested(),
                    batchState)
                : default;
            if (cancellationToken.IsCancellationRequested)
            {
                batchState.MarkExternalCancellationRequested();
            }

            for (var i = 0; i < workItems.Count; i++)
            {
                var workItem = workItems[i];
                var request = new RadarProcessingAsyncWorkerRequest(
                    workItem,
                    executor,
                    batchState,
                    enqueuedTimestamp,
                    workCancellation.Token);
                var enqueue = workers[workItem.WorkerId.Value].TryEnqueue(request);
                if (enqueue.IsAccepted)
                {
                    acceptedWorkItemCount++;
                    continue;
                }

                enqueueError = RadarProcessingAsyncWorkerGroupError.EnqueueRejected;
                batchState.RecordCompletion(RadarProcessingAsyncWorkCompletion.Canceled(workItem));
                for (var skipped = i + 1; skipped < workItems.Count; skipped++)
                {
                    batchState.RecordCompletion(RadarProcessingAsyncWorkCompletion.Canceled(workItems[skipped]));
                }

                break;
            }

            if (enqueueError == RadarProcessingAsyncWorkerGroupError.None)
            {
                (timeoutResult, healthTransition) = await WaitForTimeoutOrCompletionAsync(
                    batchState.Completion,
                    workCancellation,
                    batchState).ConfigureAwait(false);
            }

            var batchResult = await batchState.Completion.ConfigureAwait(false);
            var drainResult = CaptureDrainResult(
                acceptedWorkItemCount,
                batchResult,
                Stopwatch.GetElapsedTime(barrierStartedTimestamp),
                timeoutResult.TimedOut,
                workCancellation.IsCancellationRequested);

            if (timeoutResult.TimedOut)
            {
                return RadarProcessingAsyncWorkerGroupResult.Rejected(
                    Status,
                    RadarProcessingAsyncWorkerGroupError.TimedOut,
                    batchResult,
                    drainResult,
                    RadarProcessingAsyncFailureKind.TimedOut,
                    timeoutResult.CancellationRequested ? RadarProcessingAsyncCancellationKind.Timeout : RadarProcessingAsyncCancellationKind.None,
                    timeoutResult,
                    healthTransition);
            }

            return enqueueError == RadarProcessingAsyncWorkerGroupError.None
                ? RadarProcessingAsyncWorkerGroupResult.Completed(Status, batchResult, drainResult)
                : RadarProcessingAsyncWorkerGroupResult.Rejected(
                    Status,
                    enqueueError,
                    batchResult,
                    drainResult,
                    RadarProcessingAsyncFailureKind.EnqueueRejected);
        }
        finally
        {
            if (ownsInFlight)
            {
                Interlocked.Exchange(ref inFlight, 0);
            }
        }
    }

    /// <summary>
    /// Synchronously disposes the worker group.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes the worker group and ignores the lifecycle result.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeWithResultAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes workers and returns the lifecycle transition result.
    /// </summary>
    public async ValueTask<RadarProcessingWorkerLifecycleResult> DisposeWithResultAsync()
    {
        RadarProcessingWorkerLifecycleResult result;
        lock (lifecycleSync)
        {
            result = lifecycle.Dispose();
        }

        if (Interlocked.Exchange(ref disposeRequested, 1) == 0)
        {
            CloseWorkers();
        }

        await AwaitWorkersAsync().ConfigureAwait(false);
        if (Volatile.Read(ref disposeRequested) != 0)
        {
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }

        if (Interlocked.Exchange(ref cancellationDisposed, 1) == 0)
        {
            workerCancellation.Dispose();
        }

        return result;
    }

    private static RadarProcessingAsyncWorker[] CreateWorkers(
        RadarProcessingAsyncWorkerGroupOptions options)
    {
        var workers = new RadarProcessingAsyncWorker[options.WorkerCount];
        var mailboxOptions = new RadarProcessingWorkerMailboxOptions(options.QueueCapacity);
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new RadarProcessingAsyncWorker(new RadarProcessingWorkerId(i), mailboxOptions);
        }

        return workers;
    }

    private static RadarProcessingAsyncWorkerGroupError MapLifecycleError(
        RadarProcessingWorkerLifecycleError error) =>
        error switch
        {
            RadarProcessingWorkerLifecycleError.None => RadarProcessingAsyncWorkerGroupError.None,
            RadarProcessingWorkerLifecycleError.AlreadyStarted => RadarProcessingAsyncWorkerGroupError.AlreadyStarted,
            RadarProcessingWorkerLifecycleError.NotStarted => RadarProcessingAsyncWorkerGroupError.NotStarted,
            RadarProcessingWorkerLifecycleError.NotRunning => RadarProcessingAsyncWorkerGroupError.NotRunning,
            RadarProcessingWorkerLifecycleError.Stopping => RadarProcessingAsyncWorkerGroupError.Stopping,
            RadarProcessingWorkerLifecycleError.Stopped => RadarProcessingAsyncWorkerGroupError.Stopped,
            RadarProcessingWorkerLifecycleError.Faulted => RadarProcessingAsyncWorkerGroupError.Faulted,
            RadarProcessingWorkerLifecycleError.Disposed => RadarProcessingAsyncWorkerGroupError.Disposed,
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };

    private void CloseWorkers()
    {
        foreach (var worker in workers)
        {
            worker.Close();
        }
    }

    private async ValueTask AwaitWorkersAsync()
    {
        if (workers.Length == 0)
        {
            return;
        }

        await Task.WhenAll(workers.Select(static worker => worker.Completion)).ConfigureAwait(false);
    }

    private void MarkFaulted(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        MarkFaulted(RadarProcessingAsyncFailureKind.WorkerGroupFaulted);
    }

    private RadarProcessingWorkerGroupHealthTransition MarkFaulted(
        RadarProcessingAsyncFailureKind failureKind)
    {
        lock (lifecycleSync)
        {
            var previous = lifecycle.Status;
            var result = lifecycle.MarkFaulted();
            return new RadarProcessingWorkerGroupHealthTransition(
                previous,
                result.Status,
                failureKind);
        }
    }

    private void ValidateWorkItems(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems)
    {
        if (workItems.Count != scope.ExpectedWorkItemCount)
        {
            throw new ArgumentException("Work item count must match the batch scope.", nameof(workItems));
        }

        var seenWorkItemIds = new bool[scope.ExpectedWorkItemCount];
        for (var i = 0; i < workItems.Count; i++)
        {
            var workItem = workItems[i];
            ArgumentNullException.ThrowIfNull(workItem, nameof(workItems));

            if (workItem.BatchSequence != scope.BatchSequence)
            {
                throw new ArgumentException("Work item batch sequence must match the batch scope.", nameof(workItems));
            }

            if (workItem.TopologyVersion != scope.TopologyVersion)
            {
                throw new ArgumentException("Work item topology version must match the batch scope.", nameof(workItems));
            }

            if ((uint)workItem.WorkItemId >= (uint)scope.ExpectedWorkItemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(workItems));
            }

            if (seenWorkItemIds[workItem.WorkItemId])
            {
                throw new ArgumentException("Work item ids must be unique within the batch scope.", nameof(workItems));
            }

            if ((uint)workItem.WorkerId.Value >= (uint)Options.WorkerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(workItems), "Work item worker id is outside the worker group.");
            }

            seenWorkItemIds[workItem.WorkItemId] = true;
        }
    }

    private bool CanFitCurrentMailboxCapacity(
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems)
    {
        var perWorker = new int[Options.WorkerCount];
        foreach (var workItem in workItems)
        {
            perWorker[workItem.WorkerId.Value]++;
            if (perWorker[workItem.WorkerId.Value] > Options.QueueCapacity)
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask<(
        RadarProcessingAsyncTimeoutResult TimeoutResult,
        RadarProcessingWorkerGroupHealthTransition? HealthTransition)> WaitForTimeoutOrCompletionAsync(
        Task<RadarProcessingAsyncBatchScopeResult> completion,
        CancellationTokenSource workCancellation,
        RadarProcessingAsyncWorkerGroupBatchState batchState)
    {
        if (!Options.Execution.HasBatchTimeout)
        {
            return (RadarProcessingAsyncTimeoutResult.None, null);
        }

        var timeout = Options.Execution.BatchTimeout!.Value;
        var completed = await Task.WhenAny(
            completion,
            Task.Delay(timeout)).ConfigureAwait(false);
        if (ReferenceEquals(completed, completion))
        {
            return (RadarProcessingAsyncTimeoutResult.None, null);
        }

        var cancellationRequested = Options.Execution.TimeoutPolicy ==
            RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy;
        var healthTransition = MarkFaulted(RadarProcessingAsyncFailureKind.TimedOut);
        if (Options.Execution.TimeoutPolicy ==
            RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy)
        {
            batchState.MarkTimeoutCancellationRequested();
            await workCancellation.CancelAsync().ConfigureAwait(false);
        }

        return (
            new RadarProcessingAsyncTimeoutResult(
                timedOut: true,
                timeout: timeout,
                timeoutPolicy: Options.Execution.TimeoutPolicy,
                cancellationRequested: cancellationRequested),
            healthTransition);
    }

    private RadarProcessingAsyncWorkerGroupDrainResult CaptureDrainResult(
        int acceptedWorkItemCount = 0,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        TimeSpan barrierWaitTime = default,
        bool timedOut = false,
        bool cancellationRequested = false) =>
        new(
            acceptedWorkItemCount,
            batchResult?.Completion.RecordedWorkItemCount ?? 0,
            PendingWorkItemCount,
            RunningWorkItemCount,
            barrierWaitTime,
            timedOut,
            cancellationRequested);

    private static RadarProcessingAsyncBatchScopeResult CancelBeforeDispatch(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems)
    {
        foreach (var workItem in workItems)
        {
            scope.RecordCompletion(
                RadarProcessingAsyncWorkCompletion.Canceled(
                    workItem,
                    cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch));
        }

        return scope.Complete();
    }
}
