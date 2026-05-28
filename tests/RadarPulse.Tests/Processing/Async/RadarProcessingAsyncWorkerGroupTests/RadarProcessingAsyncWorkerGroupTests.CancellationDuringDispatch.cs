using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
    [Fact]
    public async Task CancellationWhileQueuedReturnsCanceledWorkItemWithoutRunningExecutor()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 1, queueCapacity: 2)));
        using var cancellation = new CancellationTokenSource();
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 2, workerCount: 1);
            var secondExecuted = 0;
            var dispatch = group.DispatchAsync(
                scope,
                workItems,
                async (workItem, _) =>
                {
                    if (workItem.WorkItemId == 0)
                    {
                        firstStarted.SetResult();
                        await releaseFirst.Task.ConfigureAwait(false);
                        return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                    }

                    Interlocked.Increment(ref secondExecuted);
                    return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                },
                cancellation.Token).AsTask();

            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => group.PendingWorkItemCount == 1);
            await cancellation.CancelAsync();
            releaseFirst.SetResult();

            var result = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));
            var canceled = Assert.Single(
                result.BatchResult!.Completion.Completions,
                static completion => completion.IsCanceled);

            Assert.False(result.IsSuccess);
            Assert.False(result.IsRejected);
            Assert.Equal(RadarProcessingAsyncBatchCompletionError.WorkCanceled, result.BatchResult.Error);
            Assert.Equal(RadarProcessingAsyncCancellationKind.WhileQueued, result.CancellationKind);
            Assert.Equal(RadarProcessingAsyncCancellationKind.WhileQueued, canceled.CancellationKind);
            Assert.Equal(0, secondExecuted);
            Assert.True(result.DrainResult.CancellationRequested);
            Assert.True(result.DrainResult.IsDrained);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancellationWhileRunningIsObservedAtSafeProcessingPoint()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        using var cancellation = new CancellationTokenSource();
        var startedExecution = CreateSignal();
        var observeCancellation = CreateSignal();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);
            var dispatch = group.DispatchAsync(
                scope,
                workItems,
                async (workItem, cancellationToken) =>
                {
                    startedExecution.SetResult();
                    await observeCancellation.Task.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                },
                cancellation.Token).AsTask();

            await startedExecution.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();
            observeCancellation.SetResult();

            var result = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));
            var canceled = Assert.Single(result.BatchResult!.Completion.Completions);

            Assert.False(result.IsSuccess);
            Assert.False(result.IsRejected);
            Assert.Equal(RadarProcessingAsyncBatchCompletionError.WorkCanceled, result.BatchResult.Error);
            Assert.Equal(RadarProcessingAsyncCancellationKind.WhileRunning, result.CancellationKind);
            Assert.Equal(RadarProcessingAsyncCancellationKind.WhileRunning, canceled.CancellationKind);
            Assert.True(result.DrainResult.CancellationRequested);
            Assert.True(result.DrainResult.IsDrained);
            Assert.True(group.Status.CanAcceptDispatch);
        }
        finally
        {
            observeCancellation.TrySetResult();
            await group.DisposeAsync();
        }
    }

}
