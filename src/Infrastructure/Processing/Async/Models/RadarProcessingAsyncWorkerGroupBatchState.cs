using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

internal sealed class RadarProcessingAsyncWorkerGroupBatchState
{
    private readonly object sync = new();
    private readonly TaskCompletionSource<RadarProcessingAsyncBatchScopeResult> completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int remainingWorkItemCount;
    private int externalCancellationRequested;
    private int timeoutCancellationRequested;
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

    public void MarkExternalCancellationRequested() =>
        Interlocked.Exchange(ref externalCancellationRequested, 1);

    public void MarkTimeoutCancellationRequested() =>
        Interlocked.Exchange(ref timeoutCancellationRequested, 1);

    public RadarProcessingAsyncCancellationKind GetCancellationKind(
        bool executionStarted)
    {
        if (Volatile.Read(ref timeoutCancellationRequested) != 0)
        {
            return RadarProcessingAsyncCancellationKind.Timeout;
        }

        if (Volatile.Read(ref externalCancellationRequested) != 0)
        {
            return executionStarted
                ? RadarProcessingAsyncCancellationKind.WhileRunning
                : RadarProcessingAsyncCancellationKind.WhileQueued;
        }

        return executionStarted
            ? RadarProcessingAsyncCancellationKind.WhileRunning
            : RadarProcessingAsyncCancellationKind.WhileQueued;
    }

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
