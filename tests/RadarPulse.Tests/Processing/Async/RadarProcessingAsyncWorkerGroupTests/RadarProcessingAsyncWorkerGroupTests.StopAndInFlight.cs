using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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

}
