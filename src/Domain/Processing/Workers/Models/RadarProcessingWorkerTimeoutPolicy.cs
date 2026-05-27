namespace RadarPulse.Domain.Processing;

public enum RadarProcessingWorkerTimeoutPolicy : byte
{
    Disabled = 0,
    MarkUnhealthy = 1,
    RequestCancellationAndMarkUnhealthy = 2
}
