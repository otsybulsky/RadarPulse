namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingWorkerTelemetryCounters
{
    public RadarProcessingWorkerTelemetryCounters(
        long dispatchedBatchCount = 0,
        long completedBatchCount = 0,
        long failedBatchCount = 0,
        long canceledBatchCount = 0,
        long timedOutBatchCount = 0,
        long rejectedDispatchCount = 0,
        long submittedWorkItemCount = 0,
        long acceptedWorkItemCount = 0,
        long completedWorkItemCount = 0,
        long succeededWorkItemCount = 0,
        long failedWorkItemCount = 0,
        long canceledWorkItemCount = 0,
        TimeSpan totalDispatchTime = default,
        TimeSpan totalQueueWaitTime = default,
        TimeSpan totalExecutionTime = default,
        TimeSpan totalAggregationTime = default,
        TimeSpan totalBarrierWaitTime = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dispatchedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(timedOutBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rejectedDispatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(submittedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(succeededWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledWorkItemCount);
        ThrowIfNegative(totalDispatchTime, nameof(totalDispatchTime));
        ThrowIfNegative(totalQueueWaitTime, nameof(totalQueueWaitTime));
        ThrowIfNegative(totalExecutionTime, nameof(totalExecutionTime));
        ThrowIfNegative(totalAggregationTime, nameof(totalAggregationTime));
        ThrowIfNegative(totalBarrierWaitTime, nameof(totalBarrierWaitTime));

        if (completedBatchCount > dispatchedBatchCount ||
            failedBatchCount > dispatchedBatchCount ||
            canceledBatchCount > dispatchedBatchCount ||
            timedOutBatchCount > dispatchedBatchCount ||
            rejectedDispatchCount > dispatchedBatchCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchedBatchCount),
                dispatchedBatchCount,
                "Batch counters must be covered by dispatched batch count.");
        }

        if (succeededWorkItemCount + failedWorkItemCount + canceledWorkItemCount > completedWorkItemCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedWorkItemCount),
                completedWorkItemCount,
                "Work item outcome counts must be covered by completed work item count.");
        }

        DispatchedBatchCount = dispatchedBatchCount;
        CompletedBatchCount = completedBatchCount;
        FailedBatchCount = failedBatchCount;
        CanceledBatchCount = canceledBatchCount;
        TimedOutBatchCount = timedOutBatchCount;
        RejectedDispatchCount = rejectedDispatchCount;
        SubmittedWorkItemCount = submittedWorkItemCount;
        AcceptedWorkItemCount = acceptedWorkItemCount;
        CompletedWorkItemCount = completedWorkItemCount;
        SucceededWorkItemCount = succeededWorkItemCount;
        FailedWorkItemCount = failedWorkItemCount;
        CanceledWorkItemCount = canceledWorkItemCount;
        TotalDispatchTime = totalDispatchTime;
        TotalQueueWaitTime = totalQueueWaitTime;
        TotalExecutionTime = totalExecutionTime;
        TotalAggregationTime = totalAggregationTime;
        TotalBarrierWaitTime = totalBarrierWaitTime;
    }

    public long DispatchedBatchCount { get; }

    public long CompletedBatchCount { get; }

    public long FailedBatchCount { get; }

    public long CanceledBatchCount { get; }

    public long TimedOutBatchCount { get; }

    public long RejectedDispatchCount { get; }

    public long SubmittedWorkItemCount { get; }

    public long AcceptedWorkItemCount { get; }

    public long CompletedWorkItemCount { get; }

    public long SucceededWorkItemCount { get; }

    public long FailedWorkItemCount { get; }

    public long CanceledWorkItemCount { get; }

    public TimeSpan TotalDispatchTime { get; }

    public TimeSpan TotalQueueWaitTime { get; }

    public TimeSpan TotalExecutionTime { get; }

    public TimeSpan TotalAggregationTime { get; }

    public TimeSpan TotalBarrierWaitTime { get; }

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
