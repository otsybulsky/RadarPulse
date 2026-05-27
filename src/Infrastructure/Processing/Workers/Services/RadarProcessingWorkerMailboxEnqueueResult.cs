namespace RadarPulse.Infrastructure.Processing;

public readonly record struct RadarProcessingWorkerMailboxEnqueueResult
{
    public RadarProcessingWorkerMailboxEnqueueResult(
        RadarProcessingWorkerMailboxEnqueueStatus status)
    {
        EnsureKnownStatus(status);

        Status = status;
    }

    public RadarProcessingWorkerMailboxEnqueueStatus Status { get; }

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
