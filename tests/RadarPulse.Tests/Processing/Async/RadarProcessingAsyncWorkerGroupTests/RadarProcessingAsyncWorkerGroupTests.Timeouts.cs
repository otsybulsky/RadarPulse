using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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

}
