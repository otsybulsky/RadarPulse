namespace RadarPulse.Domain.Processing;

public enum RadarProcessingDurableEnvelopeState
{
    Pending = 1,
    Claimed = 2,
    Completed = 3,
    Committed = 4,
    Failed = 5,
    Poison = 6,
    Abandoned = 7,
    Canceled = 8,
    Released = 9
}
