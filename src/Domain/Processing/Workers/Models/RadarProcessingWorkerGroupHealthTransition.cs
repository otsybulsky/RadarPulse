namespace RadarPulse.Domain.Processing;

/// <summary>
/// Captures a worker group health/status transition caused by an async failure.
/// </summary>
public sealed record RadarProcessingWorkerGroupHealthTransition
{
    /// <summary>
    /// Creates a transition between previous and current statuses with an explicit failure kind.
    /// </summary>
    public RadarProcessingWorkerGroupHealthTransition(
        RadarProcessingWorkerGroupStatus previousStatus,
        RadarProcessingWorkerGroupStatus currentStatus,
        RadarProcessingAsyncFailureKind failureKind)
    {
        ArgumentNullException.ThrowIfNull(previousStatus);
        ArgumentNullException.ThrowIfNull(currentStatus);
        EnsureKnownFailureKind(failureKind);
        if (failureKind == RadarProcessingAsyncFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Health transition requires an explicit failure kind.");
        }

        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
        FailureKind = failureKind;
    }

    /// <summary>
    /// Gets the status before the health transition.
    /// </summary>
    public RadarProcessingWorkerGroupStatus PreviousStatus { get; }

    /// <summary>
    /// Gets the status after the health transition.
    /// </summary>
    public RadarProcessingWorkerGroupStatus CurrentStatus { get; }

    /// <summary>
    /// Gets the failure kind that caused the transition.
    /// </summary>
    public RadarProcessingAsyncFailureKind FailureKind { get; }

    /// <summary>
    /// Gets whether state, health, or last error changed.
    /// </summary>
    public bool Changed =>
        PreviousStatus.State != CurrentStatus.State ||
        PreviousStatus.Health != CurrentStatus.Health ||
        PreviousStatus.LastError != CurrentStatus.LastError;

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
}
