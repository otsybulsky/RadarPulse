namespace RadarPulse.Domain.Processing;

public enum RadarSourceProcessingHandlerExecutionClassification
{
    Mergeable = 1,
    SnapshotOnly = 2,
    Unsupported = 3
}
