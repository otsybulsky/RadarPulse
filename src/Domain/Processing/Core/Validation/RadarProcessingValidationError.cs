namespace RadarPulse.Domain.Processing;

/// <summary>
/// Identifies validation failures detected while processing or auditing batch output.
/// </summary>
public enum RadarProcessingValidationError
{
    /// <summary>
    /// No validation error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// The batch used a stream schema version unsupported by the processing core.
    /// </summary>
    UnsupportedStreamSchemaVersion,

    /// <summary>
    /// The batch source universe version did not match the processing core.
    /// </summary>
    SourceUniverseVersionMismatch,

    /// <summary>
    /// An event referenced a source id outside the configured source universe.
    /// </summary>
    SourceIdOutsideUniverse,

    /// <summary>
    /// An event source id did not match its canonical source dimensions.
    /// </summary>
    SourceOwnershipMismatch,

    /// <summary>
    /// Source-local event timestamps were not applied in non-decreasing order.
    /// </summary>
    SourceOrderViolation,

    /// <summary>
    /// Actual payload value totals did not match expected totals.
    /// </summary>
    PayloadValueCountMismatch,

    /// <summary>
    /// Actual aggregate metrics did not match expected metrics.
    /// </summary>
    MetricsMismatch,

    /// <summary>
    /// Processing was canceled before validation could complete.
    /// </summary>
    Canceled
}
