namespace RadarPulse.Domain.Streaming;

public enum RadarStreamIdentityNormalizationError
{
    None = 0,
    InvalidRadarCode = 1,
    InvalidMomentName = 2,
    SourceOutOfRange = 3,
    SourceDimensionOutsideStreamEventRange = 4,
    RadarOrdinalOutsideSourceUniverse = 5,
    RadarOrdinalOutsideStreamEventRange = 6,
    MomentIdOutsideStreamEventRange = 7
}
