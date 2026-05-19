namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedSessionStatus
{
    NotStarted = 0,
    Running = 1,
    Draining = 2,
    Completed = 3,
    Faulted = 4,
    Canceled = 5,
    Disposed = 6
}
