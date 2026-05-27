namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingProductionPipelineFallbackAction
{
    None = 1,
    StopAccepting = 2,
    DrainAccepted = 3,
    CancelOpenAndRelease = 4,
    RejectUnsafeFallback = 5
}
