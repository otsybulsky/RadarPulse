namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingAsyncWorkerGroupError : byte
{
    None = 0,
    AlreadyStarted = 1,
    NotStarted = 2,
    NotRunning = 3,
    Stopping = 4,
    Stopped = 5,
    Faulted = 6,
    Disposed = 7,
    AlreadyInFlight = 8,
    EnqueueRejected = 9
}
