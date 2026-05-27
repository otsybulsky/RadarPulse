using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Shared completion barrier for one dispatched async batch.
/// </summary>
/// <remarks>
/// Workers record completions concurrently. The state completes its task once
/// every expected work item has reported, preserving the first scope-level
/// failure when completion recording itself rejects an item.
/// </remarks>
internal sealed class RadarProcessingAsyncWorkerGroupBatchState
{
    private readonly object sync = new();
    private readonly TaskCompletionSource<RadarProcessingAsyncBatchScopeResult> completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int remainingWorkItemCount;
    private int externalCancellationRequested;
    private int timeoutCancellationRequested;
    private RadarProcessingAsyncBatchScopeResult? firstFailedRecord;

    /// <summary>
    /// Creates batch state for the expected number of work items in a scope.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupBatchState(
        RadarProcessingAsyncBatchScope scope,
        int expectedWorkItemCount)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedWorkItemCount);

        Scope = scope;
        remainingWorkItemCount = expectedWorkItemCount;
    }

    /// <summary>
    /// Async batch scope that owns completion recording.
    /// </summary>
    public RadarProcessingAsyncBatchScope Scope { get; }

    /// <summary>
    /// Task completed when every expected work item has reported.
    /// </summary>
    public Task<RadarProcessingAsyncBatchScopeResult> Completion => completionSource.Task;

    /// <summary>
    /// Records that caller cancellation was requested for the dispatch.
    /// </summary>
    public void MarkExternalCancellationRequested() =>
        Interlocked.Exchange(ref externalCancellationRequested, 1);

    /// <summary>
    /// Records that timeout policy requested cancellation for the dispatch.
    /// </summary>
    public void MarkTimeoutCancellationRequested() =>
        Interlocked.Exchange(ref timeoutCancellationRequested, 1);

    /// <summary>
    /// Resolves the cancellation kind to attach to a canceled work completion.
    /// </summary>
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

    /// <summary>
    /// Records one work completion and completes the batch barrier when all work reports.
    /// </summary>
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
