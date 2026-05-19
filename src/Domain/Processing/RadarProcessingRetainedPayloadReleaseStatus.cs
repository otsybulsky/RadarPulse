namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRetainedPayloadReleaseStatus
{
    Released = 1,
    AlreadyReleased = 2,
    Failed = 3,
    NotRequired = 4
}
