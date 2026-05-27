namespace RadarPulse.Domain.Processing;

/// <summary>
/// Captures one retained async worker failure, cancellation, or timeout sample.
/// </summary>
public sealed record RadarProcessingRecentWorkerFailure
{
    /// <summary>
    /// Creates a recent worker failure sample with optional work item, worker, and shard context.
    /// </summary>
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

    /// <summary>
    /// Gets the batch sequence associated with the failure sample.
    /// </summary>
    public long BatchSequence { get; }

    /// <summary>
    /// Gets the topology version associated with the failure sample.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Gets the failure kind when the sample represents a failed work path.
    /// </summary>
    public RadarProcessingAsyncFailureKind FailureKind { get; }

    /// <summary>
    /// Gets the cancellation kind when the sample represents canceled work.
    /// </summary>
    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    /// <summary>
    /// Gets the optional work item id associated with the sample.
    /// </summary>
    public int? WorkItemId { get; }

    /// <summary>
    /// Gets the optional worker id associated with the sample.
    /// </summary>
    public RadarProcessingWorkerId? WorkerId { get; }

    /// <summary>
    /// Gets the optional shard id associated with the sample.
    /// </summary>
    public int? ShardId { get; }

    /// <summary>
    /// Gets whether timeout handling contributed to the sample.
    /// </summary>
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
