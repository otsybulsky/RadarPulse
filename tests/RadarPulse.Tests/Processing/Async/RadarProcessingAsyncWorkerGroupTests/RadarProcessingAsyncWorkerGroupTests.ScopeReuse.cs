using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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

}
