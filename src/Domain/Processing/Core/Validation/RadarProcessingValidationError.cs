namespace RadarPulse.Domain.Processing;

public enum RadarProcessingValidationError
{
    None = 0,
    UnsupportedStreamSchemaVersion,
    SourceUniverseVersionMismatch,
    SourceIdOutsideUniverse,
    SourceOwnershipMismatch,
    SourceOrderViolation,
    PayloadValueCountMismatch,
    MetricsMismatch,
    Canceled
}
