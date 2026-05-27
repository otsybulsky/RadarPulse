namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncWorkerGroupDrainResult
{
    public RadarProcessingAsyncWorkerGroupDrainResult(
        int acceptedWorkItemCount = 0,
        int completedWorkItemCount = 0,
        int pendingWorkItemCount = 0,
        int runningWorkItemCount = 0,
        TimeSpan barrierWaitTime = default,
        bool timedOut = false,
        bool cancellationRequested = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingWorkItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(runningWorkItemCount);
        if (barrierWaitTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(barrierWaitTime),
                barrierWaitTime,
                "Barrier wait time must be non-negative.");
        }

        AcceptedWorkItemCount = acceptedWorkItemCount;
        CompletedWorkItemCount = completedWorkItemCount;
        PendingWorkItemCount = pendingWorkItemCount;
        RunningWorkItemCount = runningWorkItemCount;
        BarrierWaitTime = barrierWaitTime;
        TimedOut = timedOut;
        CancellationRequested = cancellationRequested;
    }

    public int AcceptedWorkItemCount { get; }

    public int CompletedWorkItemCount { get; }

    public int PendingWorkItemCount { get; }

    public int RunningWorkItemCount { get; }

    public int OutstandingWorkItemCount => PendingWorkItemCount + RunningWorkItemCount;

    public TimeSpan BarrierWaitTime { get; }

    public bool TimedOut { get; }

    public bool CancellationRequested { get; }

    public bool IsDrained => OutstandingWorkItemCount == 0;
}
