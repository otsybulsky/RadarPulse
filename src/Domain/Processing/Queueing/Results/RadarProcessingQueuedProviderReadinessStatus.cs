namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedProviderReadinessStatus
{
    NotEvaluated = 0,
    Passed = 1,
    Failed = 2,
    Inconclusive = 3
}
