namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRetainedPayloadStrategy
{
    SnapshotCopy = 1,
    PooledCopy = 2,
    BuilderTransfer = 3
}
