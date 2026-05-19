namespace RadarPulse.Domain.Processing;

public enum RadarProcessingWorkerLifecycleError : byte
{
    None = 0,
    AlreadyStarted = 1,
    NotStarted = 2,
    NotRunning = 3,
    Stopping = 4,
    Stopped = 5,
    Faulted = 6,
    Disposed = 7
}
