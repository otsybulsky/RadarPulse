namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Failure category for radar stream identity normalization.
/// </summary>
public enum RadarStreamIdentityNormalizationError
{
    /// <summary>
    /// No normalization error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Radar code failed canonical validation.
    /// </summary>
    InvalidRadarCode = 1,

    /// <summary>
    /// Moment name failed canonical validation.
    /// </summary>
    InvalidMomentName = 2,

    /// <summary>
    /// Source dimensions were outside the source universe.
    /// </summary>
    SourceOutOfRange = 3,

    /// <summary>
    /// Source dimensions could not fit in a stream event field.
    /// </summary>
    SourceDimensionOutsideStreamEventRange = 4,

    /// <summary>
    /// Radar ordinal would exceed the configured source universe.
    /// </summary>
    RadarOrdinalOutsideSourceUniverse = 5,

    /// <summary>
    /// Radar ordinal could not fit in a stream event field.
    /// </summary>
    RadarOrdinalOutsideStreamEventRange = 6,

    /// <summary>
    /// Moment id could not fit in a stream event field.
    /// </summary>
    MomentIdOutsideStreamEventRange = 7
}
