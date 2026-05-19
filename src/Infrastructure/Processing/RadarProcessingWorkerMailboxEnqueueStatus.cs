namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingWorkerMailboxEnqueueStatus : byte
{
    Accepted = 1,
    Full = 2,
    Closed = 3,
    Disposed = 4
}
