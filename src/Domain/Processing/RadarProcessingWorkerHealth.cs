namespace RadarPulse.Domain.Processing;

public enum RadarProcessingWorkerHealth : byte
{
    NotReady = 0,
    Healthy = 1,
    Draining = 2,
    Faulted = 3,
    Disposed = 4
}
