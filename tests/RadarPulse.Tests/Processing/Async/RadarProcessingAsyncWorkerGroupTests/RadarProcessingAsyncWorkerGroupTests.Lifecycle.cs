using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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

}
