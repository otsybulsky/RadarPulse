namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingWorkerMailboxDequeueStatus : byte
{
    Item = 1,
    Closed = 2,
    Canceled = 3,
    Disposed = 4
}
