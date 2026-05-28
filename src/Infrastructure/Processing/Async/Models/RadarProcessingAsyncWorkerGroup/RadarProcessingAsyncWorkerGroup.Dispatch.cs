using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
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
}
