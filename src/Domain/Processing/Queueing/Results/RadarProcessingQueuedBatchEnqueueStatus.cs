namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedBatchEnqueueStatus
{
    Accepted = 1,
    Full = 2,
    TimedOut = 3,
    Canceled = 4,
    Closed = 5,
    Faulted = 6
}
