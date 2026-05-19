namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRecentWorkerFailure
{
    public RadarProcessingRecentWorkerFailure(
        long batchSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None,
        int? workItemId = null,
        RadarProcessingWorkerId? workerId = null,
        int? shardId = null,
        bool timedOut = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        EnsureKnownFailureKind(failureKind);
        EnsureKnownCancellationKind(cancellationKind);
        if (workItemId.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(workItemId.Value);
        }

        if (shardId.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(shardId.Value);
        }

        if (failureKind == RadarProcessingAsyncFailureKind.None &&
            cancellationKind == RadarProcessingAsyncCancellationKind.None &&
            !timedOut)
        {
            throw new ArgumentException("Worker failure sample requires a failure, cancellation, or timeout code.");
        }

        BatchSequence = batchSequence;
        TopologyVersion = topologyVersion;
        FailureKind = failureKind;
        CancellationKind = cancellationKind;
        WorkItemId = workItemId;
        WorkerId = workerId;
        ShardId = shardId;
        TimedOut = timedOut;
    }

    public long BatchSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingAsyncFailureKind FailureKind { get; }

    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    public int? WorkItemId { get; }

    public RadarProcessingWorkerId? WorkerId { get; }

    public int? ShardId { get; }

    public bool TimedOut { get; }

    private static void EnsureKnownFailureKind(
        RadarProcessingAsyncFailureKind failureKind)
    {
        if (failureKind is not RadarProcessingAsyncFailureKind.None and
            not RadarProcessingAsyncFailureKind.WorkerReportedFailure and
            not RadarProcessingAsyncFailureKind.WorkerException and
            not RadarProcessingAsyncFailureKind.DispatchRejected and
            not RadarProcessingAsyncFailureKind.EnqueueRejected and
            not RadarProcessingAsyncFailureKind.TimedOut and
            not RadarProcessingAsyncFailureKind.WorkerGroupFaulted)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }
    }

    private static void EnsureKnownCancellationKind(
        RadarProcessingAsyncCancellationKind cancellationKind)
    {
        if (cancellationKind is not RadarProcessingAsyncCancellationKind.None and
            not RadarProcessingAsyncCancellationKind.BeforeDispatch and
            not RadarProcessingAsyncCancellationKind.WhileQueued and
            not RadarProcessingAsyncCancellationKind.WhileRunning and
            not RadarProcessingAsyncCancellationKind.Timeout and
            not RadarProcessingAsyncCancellationKind.Mixed)
        {
            throw new ArgumentOutOfRangeException(nameof(cancellationKind));
        }
    }
}
