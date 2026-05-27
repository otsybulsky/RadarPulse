namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingOwnedBatchDequeueStatus
{
    Item = 1,
    Closed = 2,
    Canceled = 3,
    Faulted = 4,
    Disposed = 5
}
