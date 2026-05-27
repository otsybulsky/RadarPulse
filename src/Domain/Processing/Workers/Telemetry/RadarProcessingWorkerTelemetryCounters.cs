namespace RadarPulse.Domain.Processing;

/// <summary>
/// Carries aggregate counters and durations for async worker dispatch and execution.
/// </summary>
public sealed record RadarProcessingWorkerTelemetryCounters
{
    /// <summary>
    /// Creates worker telemetry counters and validates count and duration consistency.
    /// </summary>
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

    /// <summary>
    /// Gets the number of batches submitted to worker dispatch.
    /// </summary>
    public long DispatchedBatchCount { get; }

    /// <summary>
    /// Gets the number of dispatched batches completed by the worker group.
    /// </summary>
    public long CompletedBatchCount { get; }

    /// <summary>
    /// Gets the number of dispatched batches that failed.
    /// </summary>
    public long FailedBatchCount { get; }

    /// <summary>
    /// Gets the number of dispatched batches that were canceled.
    /// </summary>
    public long CanceledBatchCount { get; }

    /// <summary>
    /// Gets the number of dispatched batches that timed out.
    /// </summary>
    public long TimedOutBatchCount { get; }

    /// <summary>
    /// Gets the number of dispatch attempts rejected by lifecycle or capacity checks.
    /// </summary>
    public long RejectedDispatchCount { get; }

    /// <summary>
    /// Gets the total number of work items submitted for dispatch.
    /// </summary>
    public long SubmittedWorkItemCount { get; }

    /// <summary>
    /// Gets the total number of work items accepted by worker queues.
    /// </summary>
    public long AcceptedWorkItemCount { get; }

    /// <summary>
    /// Gets the total number of work items that reached a terminal completion.
    /// </summary>
    public long CompletedWorkItemCount { get; }

    /// <summary>
    /// Gets the total number of successful work item completions.
    /// </summary>
    public long SucceededWorkItemCount { get; }

    /// <summary>
    /// Gets the total number of failed work item completions.
    /// </summary>
    public long FailedWorkItemCount { get; }

    /// <summary>
    /// Gets the total number of canceled work item completions.
    /// </summary>
    public long CanceledWorkItemCount { get; }

    /// <summary>
    /// Gets total dispatch orchestration time.
    /// </summary>
    public TimeSpan TotalDispatchTime { get; }

    /// <summary>
    /// Gets total time work items spent waiting in queues.
    /// </summary>
    public TimeSpan TotalQueueWaitTime { get; }

    /// <summary>
    /// Gets total time workers spent executing work items.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets total time spent aggregating worker completions.
    /// </summary>
    public TimeSpan TotalAggregationTime { get; }

    /// <summary>
    /// Gets total time spent at the completion barrier.
    /// </summary>
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
