using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
    [Fact]
    public async Task DisposeReleasesWorkersAndRejectsLaterDispatch()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        Assert.True(group.Start().IsSuccess);

        var disposed = await group.DisposeWithResultAsync();
        Assert.True(disposed.IsSuccess);
        Assert.Equal(RadarProcessingWorkerGroupState.Disposed, disposed.Status.State);
        Assert.Equal(0, group.PendingWorkItemCount);

        var secondDispose = await group.DisposeWithResultAsync();
        Assert.True(secondDispose.IsSuccess);

        var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);
        var rejected = await group.DispatchAsync(scope, workItems, Succeed);

        Assert.True(rejected.IsRejected);
        Assert.Equal(RadarProcessingAsyncWorkerGroupError.Disposed, rejected.Error);
    }

    [Fact]
    public async Task DisposeDrainsAcceptedBorrowedWorkBeforeReleasingResources()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 1, queueCapacity: 2)));
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 2, workerCount: 1);
            var dispatch = group.DispatchAsync(
                scope,
                workItems,
                async (workItem, _) =>
                {
                    if (workItem.WorkItemId == 0)
                    {
                        firstStarted.SetResult();
                        await releaseFirst.Task.ConfigureAwait(false);
                    }

                    return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                }).AsTask();

            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => group.PendingWorkItemCount == 1);
            var dispose = group.DisposeWithResultAsync().AsTask();
            var early = await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromMilliseconds(50)));

            Assert.NotSame(dispose, early);

            releaseFirst.SetResult();
            var dispatchResult = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));
            var disposeResult = await dispose.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(dispatchResult.IsSuccess);
            Assert.Equal(2, dispatchResult.BatchResult?.Completion.SucceededWorkItemCount);
            Assert.True(dispatchResult.DrainResult.IsDrained);
            Assert.True(disposeResult.IsSuccess);
            Assert.Equal(0, group.PendingWorkItemCount);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task CapacityOverflowIsRejectedWithoutStartingBorrowedWork()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 1, queueCapacity: 1)));
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 2, workerCount: 1);
            var executed = 0;

            var result = await group.DispatchAsync(
                scope,
                workItems,
                (workItem, _) =>
                {
                    Interlocked.Increment(ref executed);
                    return Succeed(workItem, CancellationToken.None);
                });

            Assert.True(result.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.EnqueueRejected, result.Error);
            Assert.Null(result.BatchResult);
            Assert.Equal(0, result.DrainResult.AcceptedWorkItemCount);
            Assert.Equal(0, result.DrainResult.CompletedWorkItemCount);
            Assert.True(result.DrainResult.IsDrained);
            Assert.Equal(0, executed);
        }
        finally
        {
            await group.DisposeAsync();
        }
    }

}
