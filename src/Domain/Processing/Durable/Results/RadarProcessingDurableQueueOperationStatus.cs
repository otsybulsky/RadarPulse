namespace RadarPulse.Domain.Processing;

public enum RadarProcessingDurableQueueOperationStatus
{
    Accepted = 1,
    Duplicate = 2,
    Claimed = 3,
    Empty = 4,
    Completed = 5,
    Failed = 6,
    Abandoned = 7,
    Retried = 8,
    Poisoned = 9,
    Committed = 10,
    Released = 11,
    Canceled = 12,
    NotFound = 13,
    InvalidState = 14
}
