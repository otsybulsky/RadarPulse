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
        Assert.Equal(10, (int)RadarProcessingAsyncWorkerGroupError.TimedOut);
        Assert.Equal(11, (int)RadarProcessingAsyncWorkerGroupError.ScopeClosed);
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
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(
                new RadarProcessingWorkerGroupStatus(),
                new RadarProcessingAsyncBatchScopeResult(
                    new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1)),
                failureKind: (RadarProcessingAsyncFailureKind)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupResult(
                new RadarProcessingWorkerGroupStatus(),
                new RadarProcessingAsyncBatchScopeResult(
                    new RadarProcessingAsyncBatchCompletion(1, RadarProcessingTopologyVersion.Initial, 1)),
                cancellationKind: (RadarProcessingAsyncCancellationKind)255));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                new RadarProcessingWorkerGroupStatus(
                    RadarProcessingWorkerGroupState.Running,
                    RadarProcessingWorkerHealth.Healthy,
                    workerCount: 1,
                    queueCapacity: 1),
                RadarProcessingAsyncWorkerGroupError.TimedOut));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                new RadarProcessingWorkerGroupStatus(),
                RadarProcessingAsyncWorkerGroupError.NotStarted,
                timeoutResult: new RadarProcessingAsyncTimeoutResult(
                    timedOut: true,
                    timeout: TimeSpan.FromMilliseconds(1),
                    timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(acceptedWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(completedWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(pendingWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(runningWorkItemCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingAsyncWorkerGroupDrainResult(barrierWaitTime: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void WorkerGroupExposesNoFireAndForgetBorrowedBatchDispatchApi()
    {
        var dispatchMethods = typeof(RadarProcessingAsyncWorkerGroup)
            .GetMethods()
            .Where(static method => method.Name.Contains("Dispatch", StringComparison.Ordinal))
            .ToArray();

        var dispatch = Assert.Single(dispatchMethods);
        Assert.Equal(nameof(RadarProcessingAsyncWorkerGroup.DispatchAsync), dispatch.Name);
        Assert.Equal(typeof(ValueTask<RadarProcessingAsyncWorkerGroupResult>), dispatch.ReturnType);
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
            Assert.Equal(2, result.DrainResult.AcceptedWorkItemCount);
            Assert.Equal(2, result.DrainResult.CompletedWorkItemCount);
            Assert.Equal(0, result.DrainResult.OutstandingWorkItemCount);
            Assert.True(result.DrainResult.IsDrained);
            Assert.False(result.DrainResult.TimedOut);
            Assert.Equal(0, group.PendingWorkItemCount);
            Assert.Equal(0, group.RunningWorkItemCount);
            Assert.Equal(0, group.OutstandingWorkItemCount);

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

        var disposed = await group.DisposeWithResultAsync();
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
            Assert.Equal(RadarProcessingAsyncFailureKind.WorkerException, result.FailureKind);
            Assert.Equal(
                RadarProcessingAsyncFailureKind.WorkerException,
                Assert.Single(result.BatchResult.Completion.Completions).FailureKind);
            Assert.Equal(1, result.DrainResult.AcceptedWorkItemCount);
            Assert.Equal(1, result.DrainResult.CompletedWorkItemCount);
            Assert.True(result.DrainResult.IsDrained);
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
    public async Task CancellationBeforeDispatchReturnsCanceledResultWithoutBorrowedWork()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1)));
        using var cancellation = new CancellationTokenSource();
        try
        {
            Assert.True(group.Start().IsSuccess);
            await cancellation.CancelAsync();
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 2, workerCount: 2);
            var executed = 0;

            var result = await group.DispatchAsync(
                scope,
                workItems,
                (workItem, cancellationToken) =>
                {
                    Interlocked.Increment(ref executed);
                    return Succeed(workItem, cancellationToken);
                },
                cancellation.Token);

            Assert.False(result.IsSuccess);
            Assert.False(result.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.None, result.Error);
            Assert.Equal(RadarProcessingAsyncBatchCompletionError.WorkCanceled, result.BatchResult?.Error);
            Assert.Equal(RadarProcessingAsyncCancellationKind.BeforeDispatch, result.CancellationKind);
            Assert.Equal(0, result.DrainResult.AcceptedWorkItemCount);
            Assert.Equal(2, result.DrainResult.CompletedWorkItemCount);
            Assert.True(result.DrainResult.CancellationRequested);
            Assert.True(result.DrainResult.IsDrained);
            Assert.Equal(0, executed);
            Assert.All(
                result.BatchResult!.Completion.Completions,
                static completion =>
                {
                    Assert.True(completion.IsCanceled);
                    Assert.Equal(RadarProcessingAsyncCancellationKind.BeforeDispatch, completion.CancellationKind);
                });
        }
        finally
        {
            await group.DisposeAsync();
        }
    }

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
            await WaitUntilAsync(() => group.RunningWorkItemCount == 1);

            var (overlapScope, overlapWorkItems) = CreateScope(2, expectedWorkItemCount: 1, workerCount: 1);
            var overlap = await group.DispatchAsync(overlapScope, overlapWorkItems, Succeed);

            Assert.True(overlap.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.AlreadyInFlight, overlap.Error);
            Assert.Null(overlap.BatchResult);
            Assert.Equal(1, overlap.DrainResult.RunningWorkItemCount);
            Assert.Equal(1, overlap.DrainResult.OutstandingWorkItemCount);

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

    [Fact]
    public async Task CompletedBorrowedBatchScopeCannotBeReusedForLaterDispatch()
    {
        var group = new RadarProcessingAsyncWorkerGroup();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);
            var first = await group.DispatchAsync(scope, workItems, Succeed);
            Assert.True(first.IsSuccess);

            var executed = 0;
            var second = await group.DispatchAsync(
                scope,
                workItems,
                (workItem, cancellationToken) =>
                {
                    Interlocked.Increment(ref executed);
                    return Succeed(workItem, cancellationToken);
                });

            Assert.True(second.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.ScopeClosed, second.Error);
            Assert.Null(second.BatchResult);
            Assert.Equal(0, second.DrainResult.AcceptedWorkItemCount);
            Assert.True(second.DrainResult.IsDrained);
            Assert.Equal(0, executed);
        }
        finally
        {
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task TimeoutMarksGroupFaultedButDispatchReturnsOnlyAfterBorrowedWorkDrains()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(
                    workerCount: 1,
                    queueCapacity: 1,
                    timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy,
                    batchTimeout: TimeSpan.FromMilliseconds(40))));
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
            await WaitUntilAsync(() => group.Status.State == RadarProcessingWorkerGroupState.Faulted);

            Assert.False(dispatch.IsCompleted);
            Assert.Equal(1, group.RunningWorkItemCount);

            releaseExecution.SetResult();
            var result = await dispatch.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(result.IsSuccess);
            Assert.True(result.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.TimedOut, result.Error);
            Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, result.FailureKind);
            Assert.Equal(RadarProcessingAsyncCancellationKind.None, result.CancellationKind);
            Assert.NotNull(result.BatchResult);
            Assert.True(result.BatchResult.IsSuccess);
            Assert.Equal(RadarProcessingWorkerGroupState.Faulted, result.Status.State);
            Assert.True(result.TimeoutResult.TimedOut);
            Assert.Equal(TimeSpan.FromMilliseconds(40), result.TimeoutResult.Timeout);
            Assert.Equal(RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy, result.TimeoutResult.TimeoutPolicy);
            Assert.False(result.TimeoutResult.CancellationRequested);
            Assert.NotNull(result.HealthTransition);
            Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, result.HealthTransition.FailureKind);
            Assert.Equal(RadarProcessingWorkerGroupState.Running, result.HealthTransition.PreviousStatus.State);
            Assert.Equal(RadarProcessingWorkerGroupState.Faulted, result.HealthTransition.CurrentStatus.State);
            Assert.True(result.DrainResult.TimedOut);
            Assert.False(result.DrainResult.CancellationRequested);
            Assert.True(result.DrainResult.IsDrained);
            Assert.Equal(1, result.DrainResult.AcceptedWorkItemCount);
            Assert.Equal(1, result.DrainResult.CompletedWorkItemCount);
            Assert.Equal(0, group.OutstandingWorkItemCount);
        }
        finally
        {
            releaseExecution.TrySetResult();
            await group.DisposeAsync();
        }
    }

    [Fact]
    public async Task TimeoutCanRequestCooperativeCancellationAndRecordsCanceledWork()
    {
        var group = new RadarProcessingAsyncWorkerGroup(
            new RadarProcessingAsyncWorkerGroupOptions(
                new RadarProcessingAsyncExecutionOptions(
                    workerCount: 1,
                    queueCapacity: 1,
                    timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy,
                    batchTimeout: TimeSpan.FromMilliseconds(40))));
        var startedExecution = CreateSignal();
        try
        {
            Assert.True(group.Start().IsSuccess);
            var (scope, workItems) = CreateScope(1, expectedWorkItemCount: 1, workerCount: 1);

            var result = await group.DispatchAsync(
                scope,
                workItems,
                async (workItem, cancellationToken) =>
                {
                    startedExecution.SetResult();
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                    return RadarProcessingAsyncWorkCompletion.Succeeded(workItem);
                });
            var canceled = Assert.Single(result.BatchResult!.Completion.Completions);

            Assert.True(startedExecution.Task.IsCompletedSuccessfully);
            Assert.False(result.IsSuccess);
            Assert.True(result.IsRejected);
            Assert.Equal(RadarProcessingAsyncWorkerGroupError.TimedOut, result.Error);
            Assert.Equal(RadarProcessingAsyncFailureKind.TimedOut, result.FailureKind);
            Assert.Equal(RadarProcessingAsyncCancellationKind.Timeout, result.CancellationKind);
            Assert.Equal(RadarProcessingAsyncBatchCompletionError.WorkCanceled, result.BatchResult.Error);
            Assert.True(canceled.IsCanceled);
            Assert.Equal(RadarProcessingAsyncCancellationKind.Timeout, canceled.CancellationKind);
            Assert.True(result.TimeoutResult.TimedOut);
            Assert.Equal(RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy, result.TimeoutResult.TimeoutPolicy);
            Assert.True(result.TimeoutResult.CancellationRequested);
            Assert.NotNull(result.HealthTransition);
            Assert.Equal(RadarProcessingWorkerGroupState.Faulted, result.Status.State);
            Assert.Equal(RadarProcessingWorkerHealth.Faulted, result.Status.Health);
            Assert.True(result.DrainResult.TimedOut);
            Assert.True(result.DrainResult.CancellationRequested);
            Assert.True(result.DrainResult.IsDrained);
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
