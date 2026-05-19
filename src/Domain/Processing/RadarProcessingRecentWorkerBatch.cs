namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRecentWorkerBatch
{
    public RadarProcessingRecentWorkerBatch(
        long batchSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int workerCount,
        int queueCapacity,
        int submittedWorkItemCount,
        int acceptedWorkItemCount,
        int completedWorkItemCount,
        int succeededWorkItemCount,
        int failedWorkItemCount,
        int canceledWorkItemCount,
        bool isSuccessful,
        bool isRejected,
        bool timedOut,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None,
        TimeSpan dispatchTime = default,
        TimeSpan queueWaitTime = default,
        TimeSpan executionTime = default,
        TimeSpan aggregationTime = default,
        TimeSpan barrierWaitTime = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(workerCount);
        ArgumentOutOfRangeException.ThrowIfNegative(queueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(submittedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(succeededWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledWorkItemCount);
        EnsureKnownFailureKind(failureKind);
        EnsureKnownCancellationKind(cancellationKind);
        ThrowIfNegative(dispatchTime, nameof(dispatchTime));
        ThrowIfNegative(queueWaitTime, nameof(queueWaitTime));
        ThrowIfNegative(executionTime, nameof(executionTime));
        ThrowIfNegative(aggregationTime, nameof(aggregationTime));
        ThrowIfNegative(barrierWaitTime, nameof(barrierWaitTime));

        if (succeededWorkItemCount + failedWorkItemCount + canceledWorkItemCount > completedWorkItemCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedWorkItemCount),
                completedWorkItemCount,
                "Work item outcome counts must be covered by completed work item count.");
        }

        BatchSequence = batchSequence;
        TopologyVersion = topologyVersion;
        WorkerCount = workerCount;
        QueueCapacity = queueCapacity;
        SubmittedWorkItemCount = submittedWorkItemCount;
        AcceptedWorkItemCount = acceptedWorkItemCount;
        CompletedWorkItemCount = completedWorkItemCount;
        SucceededWorkItemCount = succeededWorkItemCount;
        FailedWorkItemCount = failedWorkItemCount;
        CanceledWorkItemCount = canceledWorkItemCount;
        IsSuccessful = isSuccessful;
        IsRejected = isRejected;
        TimedOut = timedOut;
        FailureKind = failureKind;
        CancellationKind = cancellationKind;
        DispatchTime = dispatchTime;
        QueueWaitTime = queueWaitTime;
        ExecutionTime = executionTime;
        AggregationTime = aggregationTime;
        BarrierWaitTime = barrierWaitTime;
    }

    public long BatchSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public int WorkerCount { get; }

    public int QueueCapacity { get; }

    public int SubmittedWorkItemCount { get; }

    public int AcceptedWorkItemCount { get; }

    public int CompletedWorkItemCount { get; }

    public int SucceededWorkItemCount { get; }

    public int FailedWorkItemCount { get; }

    public int CanceledWorkItemCount { get; }

    public bool IsSuccessful { get; }

    public bool IsRejected { get; }

    public bool TimedOut { get; }

    public RadarProcessingAsyncFailureKind FailureKind { get; }

    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    public TimeSpan DispatchTime { get; }

    public TimeSpan QueueWaitTime { get; }

    public TimeSpan ExecutionTime { get; }

    public TimeSpan AggregationTime { get; }

    public TimeSpan BarrierWaitTime { get; }

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

    private static void ThrowIfNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Duration must be non-negative.");
        }
    }
}
