using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Mailbox request that binds one async work item to an executor and batch state.
/// </summary>
internal sealed class RadarProcessingAsyncWorkerRequest
{
    /// <summary>
    /// Creates a worker request for one routed shard work item.
    /// </summary>
    public RadarProcessingAsyncWorkerRequest(
        RadarProcessingAsyncWorkItem workItem,
        RadarProcessingAsyncWorkExecutor executor,
        RadarProcessingAsyncWorkerGroupBatchState batchState,
        long enqueuedTimestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(batchState);

        WorkItem = workItem;
        Executor = executor;
        BatchState = batchState;
        EnqueuedTimestamp = enqueuedTimestamp;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Work item assigned by the dispatch plan.
    /// </summary>
    public RadarProcessingAsyncWorkItem WorkItem { get; }

    /// <summary>
    /// Delegate executed by the worker.
    /// </summary>
    public RadarProcessingAsyncWorkExecutor Executor { get; }

    /// <summary>
    /// Shared batch state that receives the completion.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupBatchState BatchState { get; }

    /// <summary>
    /// Stopwatch timestamp captured before enqueue for queue-wait measurement.
    /// </summary>
    public long EnqueuedTimestamp { get; }

    /// <summary>
    /// Dispatch-scoped cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
