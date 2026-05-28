using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroupTests
{
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
