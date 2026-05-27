namespace RadarPulse.Domain.Processing;

public enum RadarProcessingHandlerDeltaMergeStatus
{
    Accepted = 1,
    Duplicate = 2,
    Rejected = 3,
    Blocked = 4
}
