namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingAsyncWorkCompletion
{
    public RadarProcessingAsyncWorkCompletion(
        long batchSequence,
        int workItemId,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingWorkerId workerId,
        RadarProcessingAsyncWorkStatus status,
        TimeSpan queueWaitTime = default,
        TimeSpan executionTime = default,
        long processedStreamEventCount = 0,
        long processedPayloadValueCount = 0,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(workItemId);
        EnsureKnownStatus(status);
        EnsureKnownFailureKind(failureKind);
        EnsureKnownCancellationKind(cancellationKind);
        ThrowIfNegative(queueWaitTime, nameof(queueWaitTime));
        ThrowIfNegative(executionTime, nameof(executionTime));
        ArgumentOutOfRangeException.ThrowIfNegative(processedStreamEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(processedPayloadValueCount);
        if (status == RadarProcessingAsyncWorkStatus.Failed &&
            failureKind == RadarProcessingAsyncFailureKind.None)
        {
            throw new ArgumentException("Failed work completion requires a failure kind.", nameof(failureKind));
        }

        if (status != RadarProcessingAsyncWorkStatus.Failed &&
            failureKind != RadarProcessingAsyncFailureKind.None)
        {
            throw new ArgumentException("Only failed work completion can carry a failure kind.", nameof(failureKind));
        }

        if (status != RadarProcessingAsyncWorkStatus.Canceled &&
            cancellationKind != RadarProcessingAsyncCancellationKind.None)
        {
            throw new ArgumentException(
                "Only canceled work completion can carry a cancellation kind.",
                nameof(cancellationKind));
        }

        BatchSequence = batchSequence;
        WorkItemId = workItemId;
        TopologyVersion = topologyVersion;
        WorkerId = workerId;
        Status = status;
        QueueWaitTime = queueWaitTime;
        ExecutionTime = executionTime;
        ProcessedStreamEventCount = processedStreamEventCount;
        ProcessedPayloadValueCount = processedPayloadValueCount;
        FailureKind = failureKind;
        CancellationKind = cancellationKind;
    }

    public long BatchSequence { get; }

    public int WorkItemId { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingWorkerId WorkerId { get; }

    public RadarProcessingAsyncWorkStatus Status { get; }

    public TimeSpan QueueWaitTime { get; }

    public TimeSpan ExecutionTime { get; }

    public long ProcessedStreamEventCount { get; }

    public long ProcessedPayloadValueCount { get; }

    public RadarProcessingAsyncFailureKind FailureKind { get; }

    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    public bool IsSuccessful => Status == RadarProcessingAsyncWorkStatus.Succeeded;

    public bool IsFailed => Status == RadarProcessingAsyncWorkStatus.Failed;

    public bool IsCanceled => Status == RadarProcessingAsyncWorkStatus.Canceled;

    public static RadarProcessingAsyncWorkCompletion Succeeded(
        RadarProcessingAsyncWorkItem workItem,
        TimeSpan queueWaitTime = default,
        TimeSpan executionTime = default,
        long processedStreamEventCount = 0,
        long processedPayloadValueCount = 0) =>
        FromWorkItem(
            workItem,
            RadarProcessingAsyncWorkStatus.Succeeded,
            queueWaitTime,
            executionTime,
            processedStreamEventCount,
            processedPayloadValueCount);

    public static RadarProcessingAsyncWorkCompletion Failed(
        RadarProcessingAsyncWorkItem workItem,
        TimeSpan queueWaitTime = default,
        TimeSpan executionTime = default,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.WorkerReportedFailure) =>
        FromWorkItem(
            workItem,
            RadarProcessingAsyncWorkStatus.Failed,
            queueWaitTime,
            executionTime,
            failureKind: failureKind);

    public static RadarProcessingAsyncWorkCompletion Canceled(
        RadarProcessingAsyncWorkItem workItem,
        TimeSpan queueWaitTime = default,
        TimeSpan executionTime = default,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None) =>
        FromWorkItem(
            workItem,
            RadarProcessingAsyncWorkStatus.Canceled,
            queueWaitTime,
            executionTime,
            cancellationKind: cancellationKind);

    internal static void EnsureKnownStatus(
        RadarProcessingAsyncWorkStatus status)
    {
        if (status is not RadarProcessingAsyncWorkStatus.Succeeded and
            not RadarProcessingAsyncWorkStatus.Failed and
            not RadarProcessingAsyncWorkStatus.Canceled)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    internal static void EnsureKnownFailureKind(
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

    internal static void EnsureKnownCancellationKind(
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

    private static RadarProcessingAsyncWorkCompletion FromWorkItem(
        RadarProcessingAsyncWorkItem workItem,
        RadarProcessingAsyncWorkStatus status,
        TimeSpan queueWaitTime,
        TimeSpan executionTime,
        long processedStreamEventCount = 0,
        long processedPayloadValueCount = 0,
        RadarProcessingAsyncFailureKind failureKind = RadarProcessingAsyncFailureKind.None,
        RadarProcessingAsyncCancellationKind cancellationKind = RadarProcessingAsyncCancellationKind.None)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        return new RadarProcessingAsyncWorkCompletion(
            workItem.BatchSequence,
            workItem.WorkItemId,
            workItem.TopologyVersion,
            workItem.WorkerId,
            status,
            queueWaitTime,
            executionTime,
            processedStreamEventCount,
            processedPayloadValueCount,
            failureKind,
            cancellationKind);
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
