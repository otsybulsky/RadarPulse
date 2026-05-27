namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRetainedPayloadRetentionStatus
{
    Succeeded = 1,
    UnsupportedStrategy = 2,
    FailedCopy = 3,
    Canceled = 4,
    InvalidInput = 5
}
