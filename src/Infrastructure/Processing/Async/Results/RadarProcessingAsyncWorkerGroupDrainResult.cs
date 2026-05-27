namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Drain and outstanding-work evidence captured around async worker dispatch.
/// </summary>
public sealed record RadarProcessingAsyncWorkerGroupDrainResult
{
    /// <summary>
    /// Creates a drain result with non-negative counts and barrier wait time.
    /// </summary>
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

    /// <summary>
    /// Number of work items accepted into worker mailboxes.
    /// </summary>
    public int AcceptedWorkItemCount { get; }

    /// <summary>
    /// Number of work items that recorded completions.
    /// </summary>
    public int CompletedWorkItemCount { get; }

    /// <summary>
    /// Number of work items still pending in mailboxes when captured.
    /// </summary>
    public int PendingWorkItemCount { get; }

    /// <summary>
    /// Number of work items still running when captured.
    /// </summary>
    public int RunningWorkItemCount { get; }

    /// <summary>
    /// Number of work items not yet drained.
    /// </summary>
    public int OutstandingWorkItemCount => PendingWorkItemCount + RunningWorkItemCount;

    /// <summary>
    /// Time spent waiting for the batch completion barrier.
    /// </summary>
    public TimeSpan BarrierWaitTime { get; }

    /// <summary>
    /// Indicates whether timeout policy fired before the barrier completed.
    /// </summary>
    public bool TimedOut { get; }

    /// <summary>
    /// Indicates whether dispatch cancellation was requested.
    /// </summary>
    public bool CancellationRequested { get; }

    /// <summary>
    /// Indicates whether no accepted work remains pending or running.
    /// </summary>
    public bool IsDrained => OutstandingWorkItemCount == 0;
}
