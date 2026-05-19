using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private readonly object lifecycleSync = new();
    private readonly RadarProcessingAsyncWorker[] workers;
    private readonly RadarProcessingWorkerGroupLifecycle lifecycle;
    private readonly CancellationTokenSource workerCancellation = new();
    private int inFlight;
    private int disposeRequested;
    private int cancellationDisposed;

    public RadarProcessingAsyncWorkerGroup(
        RadarProcessingAsyncWorkerGroupOptions? options = null)
    {
        Options = options ?? RadarProcessingAsyncWorkerGroupOptions.Default;
        lifecycle = new RadarProcessingWorkerGroupLifecycle(Options.Execution);
        workers = CreateWorkers(Options);
    }

    public RadarProcessingAsyncWorkerGroupOptions Options { get; }

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

    public int PendingWorkItemCount => workers.Sum(static worker => worker.PendingCount);

    public int RunningWorkItemCount => workers.Sum(static worker => worker.RunningCount);

    public int OutstandingWorkItemCount => PendingWorkItemCount + RunningWorkItemCount;

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

    public async ValueTask<RadarProcessingAsyncWorkerGroupResult> DispatchAsync(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        RadarProcessingAsyncWorkExecutor executor,
        CancellationToken cancellationToken = default)
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

        if (Interlocked.CompareExchange(ref inFlight, 1, 0) != 0)
        {
            return RadarProcessingAsyncWorkerGroupResult.Rejected(
                Status,
                RadarProcessingAsyncWorkerGroupError.AlreadyInFlight,
                drainResult: CaptureDrainResult());
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
            var timedOut = false;
            using var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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
                timedOut = await WaitForTimeoutOrCompletionAsync(
                    batchState.Completion,
                    workCancellation).ConfigureAwait(false);
            }

            var batchResult = await batchState.Completion.ConfigureAwait(false);
            var drainResult = CaptureDrainResult(
                acceptedWorkItemCount,
                batchResult,
                Stopwatch.GetElapsedTime(barrierStartedTimestamp),
                timedOut,
                workCancellation.IsCancellationRequested);

            if (timedOut)
            {
                return RadarProcessingAsyncWorkerGroupResult.Rejected(
                    Status,
                    RadarProcessingAsyncWorkerGroupError.TimedOut,
                    batchResult,
                    drainResult);
            }

            return enqueueError == RadarProcessingAsyncWorkerGroupError.None
                ? RadarProcessingAsyncWorkerGroupResult.Completed(Status, batchResult, drainResult)
                : RadarProcessingAsyncWorkerGroupResult.Rejected(Status, enqueueError, batchResult, drainResult);
        }
        finally
        {
            Interlocked.Exchange(ref inFlight, 0);
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask<RadarProcessingWorkerLifecycleResult> DisposeAsync()
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

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsync().ConfigureAwait(false);
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

        MarkFaulted();
    }

    private void MarkFaulted()
    {
        lock (lifecycleSync)
        {
            lifecycle.MarkFaulted();
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

    private async ValueTask<bool> WaitForTimeoutOrCompletionAsync(
        Task<RadarProcessingAsyncBatchScopeResult> completion,
        CancellationTokenSource workCancellation)
    {
        if (!Options.Execution.HasBatchTimeout)
        {
            return false;
        }

        var timeout = Options.Execution.BatchTimeout!.Value;
        var completed = await Task.WhenAny(
            completion,
            Task.Delay(timeout)).ConfigureAwait(false);
        if (ReferenceEquals(completed, completion))
        {
            return false;
        }

        MarkFaulted();
        if (Options.Execution.TimeoutPolicy ==
            RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy)
        {
            await workCancellation.CancelAsync().ConfigureAwait(false);
        }

        return true;
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
}
