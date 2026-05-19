using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

internal sealed class RadarProcessingAsyncWorkerRequest
{
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

    public RadarProcessingAsyncWorkItem WorkItem { get; }

    public RadarProcessingAsyncWorkExecutor Executor { get; }

    public RadarProcessingAsyncWorkerGroupBatchState BatchState { get; }

    public long EnqueuedTimestamp { get; }

    public CancellationToken CancellationToken { get; }
}
