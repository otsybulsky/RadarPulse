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
        long processedPayloadValueCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(workItemId);
        EnsureKnownStatus(status);
        ThrowIfNegative(queueWaitTime, nameof(queueWaitTime));
        ThrowIfNegative(executionTime, nameof(executionTime));
        ArgumentOutOfRangeException.ThrowIfNegative(processedStreamEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(processedPayloadValueCount);

        BatchSequence = batchSequence;
        WorkItemId = workItemId;
        TopologyVersion = topologyVersion;
        WorkerId = workerId;
        Status = status;
        QueueWaitTime = queueWaitTime;
        ExecutionTime = executionTime;
        ProcessedStreamEventCount = processedStreamEventCount;
        ProcessedPayloadValueCount = processedPayloadValueCount;
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
        TimeSpan executionTime = default) =>
        FromWorkItem(
            workItem,
            RadarProcessingAsyncWorkStatus.Failed,
            queueWaitTime,
            executionTime);

    public static RadarProcessingAsyncWorkCompletion Canceled(
        RadarProcessingAsyncWorkItem workItem,
        TimeSpan queueWaitTime = default,
        TimeSpan executionTime = default) =>
        FromWorkItem(
            workItem,
            RadarProcessingAsyncWorkStatus.Canceled,
            queueWaitTime,
            executionTime);

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

    private static RadarProcessingAsyncWorkCompletion FromWorkItem(
        RadarProcessingAsyncWorkItem workItem,
        RadarProcessingAsyncWorkStatus status,
        TimeSpan queueWaitTime,
        TimeSpan executionTime,
        long processedStreamEventCount = 0,
        long processedPayloadValueCount = 0)
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
            processedPayloadValueCount);
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
