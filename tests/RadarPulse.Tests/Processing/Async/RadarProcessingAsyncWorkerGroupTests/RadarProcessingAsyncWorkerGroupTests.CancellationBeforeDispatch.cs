using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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

}
