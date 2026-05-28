using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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

}
