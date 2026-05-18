namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQuarantineEffectiveClassification
{
    None = 0,
    MovableHot = 1,
    IntrinsicHot = 2,
    Quarantined = 3,
    RetryEligible = 4
}
