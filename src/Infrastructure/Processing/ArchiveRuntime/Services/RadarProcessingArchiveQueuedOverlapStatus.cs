namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingArchiveQueuedOverlapStatus
{
    NotStarted = 0,
    Completed = 1,
    ProducerFailed = 2,
    ConsumerFaulted = 3,
    Canceled = 4,
    Disposed = 5
}
