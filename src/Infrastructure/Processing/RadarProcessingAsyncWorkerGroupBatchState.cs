using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

internal sealed class RadarProcessingAsyncWorkerGroupBatchState
{
    private readonly object sync = new();
    private readonly TaskCompletionSource<RadarProcessingAsyncBatchScopeResult> completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int remainingWorkItemCount;
    private RadarProcessingAsyncBatchScopeResult? firstFailedRecord;

    public RadarProcessingAsyncWorkerGroupBatchState(
        RadarProcessingAsyncBatchScope scope,
        int expectedWorkItemCount)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedWorkItemCount);

        Scope = scope;
        remainingWorkItemCount = expectedWorkItemCount;
    }

    public RadarProcessingAsyncBatchScope Scope { get; }

    public Task<RadarProcessingAsyncBatchScopeResult> Completion => completionSource.Task;

    public void RecordCompletion(
        RadarProcessingAsyncWorkCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        RadarProcessingAsyncBatchScopeResult? completed = null;
        lock (sync)
        {
            var record = Scope.RecordCompletion(completion);
            if (!record.IsSuccess && firstFailedRecord is null)
            {
                firstFailedRecord = record;
            }

            remainingWorkItemCount--;
            if (remainingWorkItemCount == 0)
            {
                completed = firstFailedRecord ?? Scope.Complete();
            }
        }

        if (completed is not null)
        {
            completionSource.TrySetResult(completed);
        }
    }
}
