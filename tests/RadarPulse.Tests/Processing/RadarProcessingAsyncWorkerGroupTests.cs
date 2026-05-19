using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingAsyncWorkerGroupTests
{
    [Fact]
    public void WorkerGroupErrorEnumValuesAreStable()
    {
        Assert.Equal(0, (int)RadarProcessingAsyncWorkerGroupError.None);
        Assert.Equal(1, (int)RadarProcessingAsyncWorkerGroupError.AlreadyStarted);
        Assert.Equal(2, (int)RadarProcessingAsyncWorkerGroupError.NotStarted);
        Assert.Equal(3, (int)RadarProcessingAsyncWorkerGroupError.NotRunning);
        Assert.Equal(4, (int)RadarProcessingAsyncWorkerGroupError.Stopping);
        Assert.Equal(5, (int)RadarProcessingAsyncWorkerGroupError.Stopped);
        Assert.Equal(6, (int)RadarProcessingAsyncWorkerGroupError.Faulted);
        Assert.Equal(7, (int)RadarProcessingAsyncWorkerGroupError.Disposed);
        Assert.Equal(8, (int)RadarProcessingAsyncWorkerGroupError.AlreadyInFlight);
        Assert.Equal(9, (int)RadarProcessingAsyncWorkerGroupError.EnqueueRejected);
    }

    [Fact]
    public void WorkerGroupOptionsUseExecutionDefaults()
    {
        var options = RadarProcessingAsyncWorkerGroupOptions.Default;

        Assert.Same(RadarProcessingAsyncExecutionOptions.Default, options.Execution);
        Assert.Equal(RadarProcessingAsyncExecutionOptions.Default.WorkerCount, options.WorkerCount);
        Assert.Equal(RadarProcessingAsyncExecutionOptions.Default.QueueCapacity, options.QueueCapacity);
        Assert.Throws<ArgumentNullException>(() => new RadarProcessingAsyncWorkerGroupResult(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(
                new RadarProcessingWorkerGroupStatus(),
                new RadarProcessingAsyncBatchScopeResult(
                    new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1)),
                (RadarProcessingAsyncWorkerGroupError)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(new RadarProcessingWorkerGroupStatus()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                new RadarProcessingWorkerGroupStatus(),
                RadarProcessingAsyncWorkerGroupError.None));
    }

    [Fact]
    public async Task WorkersProcessAcceptedWorkAndStopDeterministically()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1)));
        try
        {
            var started = group.Start();
            Assert.True(started.IsSuccess);
            Assert.Equal(RadarProcessingWorkerGroupState.Running, started.Status.State);

            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 2, workerCount: 2);
            var result = await group.DispatchAsync(scope, workItems, Succeed);

            Assert.True(result.IsSuccess);
            Assert.False(result.IsRejected);
            Assert.NotNull(result.BatchResult);
            Assert.Equal(2, result.BatchResult.Completion.SucceededWorkItemCount);
            Assert.Equal(2, result.BatchResult.Completion.ProcessedStreamEventCount);
            Assert.Equal(4, result.BatchResult.Completion.ProcessedPayloadValueCount);
            Assert.Equal(0, group.PendingWorkItemCount);

            var stopped = await group.StopAsync();
            Assert.True(stopped.IsSuccess);
            Assert.Equal(RadarProcessingWorkerGroupState.Stopped, stopped.Status.State);
        }
        finally
        {
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task DispatchBeforeStartAndAfterDisposeIsRejected()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);

        var beforeStart = await group.DispatchAsync(scope, workItems, Succeed);
        Assert.True(beforeStart.IsRejected);
        Assert.Equal(RadarProcessingAsyncWorkerGroupError.NotStarted, beforeStart.Error);
        Assert.Null(beforeStart.BatchResult);

        var disposed = await group.DisposeAsync();
        Assert.True(disposed.IsSuccess);

        var (disposedScope, disposedWorkItems) = CreateScope(2, expectedWorkItemCount: 1, workerCount: 1);
        var afterDispose = await group.DispatchAsync(disposedScope, disposedWorkItems, Succeed);
        Assert.True(afterDispose.IsRejected);
        Assert.Equal(RadarProcessingAsyncWorkerGroupError.Disposed, afterDispose.Error);
        Assert.Null(afterDispose.BatchResult);
    }

    [Fact]
    public async Task WorkerExceptionFailsBatchWithoutFaultingWorkerLoop()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);

            var result = await group.DispatchAsync(scope, workItems, Throw);

            Assert.False(result.IsSuccess);
            Assert.False(result.IsRejected);
            Assert.NotNull(result.BatchResult);
            Assert.Equal(RadarProcessingAsyncBatchCompletionError.WorkFailed, result.BatchResult.Error);
            Assert.Equal(1, result.BatchResult.Completion.FailedWorkItemCount);
            Assert.True(group.Status.CanAcceptDispatch);

            var (nextScope, nextWorkItems) = CreateScope(2, expectedWorkItemCount: 1, workerCount: 1);
            var next = await group.DispatchAsync(nextScope, nextWorkItems, Succeed);
            Assert.True(next.IsSuccess);
        }
        finally
        {
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task StopAcceptingRejectsNewDispatchWhileAcceptedWorkDrains()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        var startedExecution = CreateSignal();
        var releaseExecution = CreateSignal();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);
            var dispatch = group.DispatchAsync(
                scope,
                workItems,
                async (workItem, _) =>
                {
                    startedExecution.SetResult();
                    await releaseExecution.Task.ConfigureAwait(false);
                    return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                }).AsTask();

            await startedExecution.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = group.StopAccepting();
            Assert.True(stopping.IsSuccess);
            Assert.Equal(RadarProcessingWorkerGroupState.Stopping, stopping.Status.State);

            var (secondScope, secondWorkItems) = CreateScope(2, expectedWorkItemCount: 1, workerCount: 1);
            var rejected = await group.DispatchAsync(secondScope, secondWorkItems, Succeed);
            Assert.True(rejected.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.Stopping, rejected.Error);

            releaseExecution.SetResult();
            var drained = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(drained.IsSuccess);

            var stopped = await group.StopAsync();
            Assert.True(stopped.IsSuccess);
            Assert.Equal(RadarProcessingWorkerGroupState.Stopped, stopped.Status.State);
        }
        finally
        {
            releaseExecution.TrySetResult();
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task OneInFlightRuleRejectsOverlappingBorrowedBatchDispatch()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        var startedExecution = CreateSignal();
        var releaseExecution = CreateSignal();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);
            var firstDispatch = group.DispatchAsync(
                scope,
                workItems,
                async (workItem, _) =>
                {
                    startedExecution.SetResult();
                    await releaseExecution.Task.ConfigureAwait(false);
                    return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                }).AsTask();

            await startedExecution.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var (overlapScope, overlapWorkItems) = CreateScope(2, expectedWorkItemCount: 1, workerCount: 1);
            var overlap = await group.DispatchAsync(overlapScope, overlapWorkItems, Succeed);

            Assert.True(overlap.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.AlreadyInFlight, overlap.Error);
            Assert.Null(overlap.BatchResult);

            releaseExecution.SetResult();
            var first = await firstDispatch.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(first.IsSuccess);
        }
        finally
        {
            releaseExecution.TrySetResult();
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeReleasesWorkersAndRejectsLaterDispatch()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        Assert.True(group.Start().IsSuccess);

        var disposed = await group.DisposeAsync();
        Assert.True(disposed.IsSuccess);
        Assert.Equal(RadarProcessingWorkerGroupState.Disposed, disposed.Status.State);
        Assert.Equal(0, group.PendingWorkItemCount);

        var secondDispose = await group.DisposeAsync();
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
            var dispose = group.DisposeAsync().AsTask();
            var early = await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromMilliseconds(50)));

            Assert.NotSame(dispose, early);

            releaseFirst.SetResult();
            var dispatchResult = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));
            var disposeResult = await dispose.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(dispatchResult.IsSuccess);
            Assert.Equal(2, dispatchResult.BatchResult?.Completion.SucceededWorkItemCount);
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
            Assert.Equal(0, executed);
        }
        finally
        {
            await group.DisposeAsync();
        }
    }

    private static ValueTask<RadarProcessingAsyncWorkCompletion> Succeed(
        RadarProcessingAsyncWorkItem workItem,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            RadarProcessingAsyncWorkCompletion.Succeeded(
                workItem,
                processedStreamEventCount: 1,
                processedPayloadValueCount: 2));
    }

    private static ValueTask<RadarProcessingAsyncWorkCompletion> Throw(
        RadarProcessingAsyncWorkItem workItem,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Worker failure should be recorded as a failed work item.");

    private static (
        RadarProcessingAsyncBatchScope Scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> WorkItems) CreateScope(
        long batchSequence,
        int expectedWorkItemCount,
        int workerCount)
    {
        var scope = new RadarProcessingAsyncBatchScope(
            batchSequence,
            RadarProcessingTopologyVersion.Initial,
            expectedWorkItemCount);
        var workItems = new RadarProcessingAsyncWorkItem[expectedWorkItemCount];
        for (var i = 0; i < workItems.Length; i++)
        {
            workItems[i] = scope.CreateWorkItem(
                i,
                new RadarProcessingWorkerId(i % workerCount),
                shardId: i % workerCount,
                new[] { i });
        }

        return (scope, workItems);
    }

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(
        Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met before the test timeout.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
}
