namespace RadarPulse.Domain.Processing;

public enum RadarProcessingWorkerGroupState : byte
{
    NotStarted = 0,
    Running = 1,
    Stopping = 2,
    Stopped = 3,
    Faulted = 4,
    Disposed = 5
}
