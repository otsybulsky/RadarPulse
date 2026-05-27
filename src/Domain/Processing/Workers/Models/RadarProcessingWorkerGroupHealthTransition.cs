namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingWorkerGroupHealthTransition
{
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

    public RadarProcessingWorkerGroupStatus PreviousStatus { get; }

    public RadarProcessingWorkerGroupStatus CurrentStatus { get; }

    public RadarProcessingAsyncFailureKind FailureKind { get; }

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
