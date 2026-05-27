namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQuarantineTransitionReason
{
    None = 0,
    EnteredQuarantine = 1,
    MarkedRetryEligibleByTtl = 2,
    MarkedRetryEligibleBySustainedCooling = 3,
    MarkedRetryEligibleByPressureChange = 4,
    ClearedBySustainedCooling = 5,
    ClearedByEffectiveRelief = 6,
    ClearedExplicitly = 7,
    ReenteredQuarantine = 8
}
