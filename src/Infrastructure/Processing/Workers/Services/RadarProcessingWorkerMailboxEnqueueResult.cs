namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result returned by worker mailbox enqueue attempts.
/// </summary>
/// <remarks>
/// The result is status-only because the mailbox never takes ownership of a
/// rejected work item; callers retain the original object and decide whether to
/// retry, fail, or cancel the surrounding dispatch.
/// </remarks>
public readonly record struct RadarProcessingWorkerMailboxEnqueueResult
{
    /// <summary>
    /// Creates an enqueue result for a known mailbox status.
    /// </summary>
    public RadarProcessingWorkerMailboxEnqueueResult(
        RadarProcessingWorkerMailboxEnqueueStatus status)
    {
        EnsureKnownStatus(status);

        Status = status;
    }

    /// <summary>
    /// Status reported by the mailbox writer.
    /// </summary>
    public RadarProcessingWorkerMailboxEnqueueStatus Status { get; }

    /// <summary>
    /// Indicates whether the mailbox accepted the work item.
    /// </summary>
    public bool IsAccepted => Status == RadarProcessingWorkerMailboxEnqueueStatus.Accepted;

    internal static void EnsureKnownStatus(
        RadarProcessingWorkerMailboxEnqueueStatus status)
    {
        if (status is not RadarProcessingWorkerMailboxEnqueueStatus.Accepted and
            not RadarProcessingWorkerMailboxEnqueueStatus.Full and
            not RadarProcessingWorkerMailboxEnqueueStatus.Closed and
            not RadarProcessingWorkerMailboxEnqueueStatus.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
