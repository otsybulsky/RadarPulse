namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingProductionPipelineRunState
{
    NotStarted = 1,
    Running = 2,
    Draining = 3,
    Completed = 4,
    Stopped = 5,
    Blocked = 6,
    Failed = 7,
    Canceled = 8
}
