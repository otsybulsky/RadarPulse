namespace RadarPulse.Domain.Processing;

/// <summary>
/// Captures one retained async worker batch sample for recent diagnostic telemetry.
/// </summary>
public sealed record RadarProcessingRecentWorkerBatch
{
    /// <summary>
    /// Creates a recent worker batch sample and validates counters, durations, and outcome codes.
    /// </summary>
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

    /// <summary>
    /// Gets the batch sequence represented by the sample.
    /// </summary>
    public long BatchSequence { get; }

    /// <summary>
    /// Gets the topology version used by the sampled batch.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Gets the worker count used for the sampled batch.
    /// </summary>
    public int WorkerCount { get; }

    /// <summary>
    /// Gets the queue capacity used for the sampled batch.
    /// </summary>
    public int QueueCapacity { get; }

    /// <summary>
    /// Gets the number of work items submitted for the batch.
    /// </summary>
    public int SubmittedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of work items accepted by worker queues.
    /// </summary>
    public int AcceptedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of work items completed with any terminal status.
    /// </summary>
    public int CompletedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of successful work item completions.
    /// </summary>
    public int SucceededWorkItemCount { get; }

    /// <summary>
    /// Gets the number of failed work item completions.
    /// </summary>
    public int FailedWorkItemCount { get; }

    /// <summary>
    /// Gets the number of canceled work item completions.
    /// </summary>
    public int CanceledWorkItemCount { get; }

    /// <summary>
    /// Gets whether the sampled batch completed successfully.
    /// </summary>
    public bool IsSuccessful { get; }

    /// <summary>
    /// Gets whether dispatch was rejected for the sampled batch.
    /// </summary>
    public bool IsRejected { get; }

    /// <summary>
    /// Gets whether timeout handling fired for the sampled batch.
    /// </summary>
    public bool TimedOut { get; }

    /// <summary>
    /// Gets the failure kind recorded for the sampled batch.
    /// </summary>
    public RadarProcessingAsyncFailureKind FailureKind { get; }

    /// <summary>
    /// Gets the cancellation kind recorded for the sampled batch.
    /// </summary>
    public RadarProcessingAsyncCancellationKind CancellationKind { get; }

    /// <summary>
    /// Gets dispatch orchestration time for the sampled batch.
    /// </summary>
    public TimeSpan DispatchTime { get; }

    /// <summary>
    /// Gets total queue wait time for the sampled batch.
    /// </summary>
    public TimeSpan QueueWaitTime { get; }

    /// <summary>
    /// Gets total worker execution time for the sampled batch.
    /// </summary>
    public TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Gets completion aggregation time for the sampled batch.
    /// </summary>
    public TimeSpan AggregationTime { get; }

    /// <summary>
    /// Gets completion barrier wait time for the sampled batch.
    /// </summary>
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
