namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports whether processing output passed validation and carries optional comparison metrics.
/// </summary>
/// <param name="IsValid">True when no validation error was detected.</param>
/// <param name="Error">Validation error code, or <see cref="RadarProcessingValidationError.None"/>.</param>
/// <param name="SourceId">Source id associated with the error, or -1 for batch-level failures.</param>
/// <param name="EventIndex">Batch event index associated with the error, or -1 for aggregate failures.</param>
/// <param name="Message">Diagnostic validation message.</param>
/// <param name="Metrics">Actual metrics observed during validation.</param>
/// <param name="ExpectedMetrics">Expected metrics when validation compared against an independent projection.</param>
public sealed record RadarProcessingValidationResult(
    bool IsValid,
    RadarProcessingValidationError Error,
    int SourceId,
    int EventIndex,
    string Message,
    RadarProcessingMetrics Metrics,
    RadarProcessingMetrics? ExpectedMetrics)
{
    /// <summary>
    /// Creates a successful validation result for the supplied metrics.
    /// </summary>
    public static RadarProcessingValidationResult Valid(RadarProcessingMetrics metrics) =>
        new(
            true,
            RadarProcessingValidationError.None,
            SourceId: -1,
            EventIndex: -1,
            Message: string.Empty,
            metrics,
            ExpectedMetrics: null);

    /// <summary>
    /// Creates a failed validation result with location and optional expected metrics evidence.
    /// </summary>
    public static RadarProcessingValidationResult Invalid(
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message,
        RadarProcessingMetrics metrics = default,
        RadarProcessingMetrics? expectedMetrics = null)
    {
        if (error == RadarProcessingValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        if (sourceId < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceId));
        }

        if (eventIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(eventIndex));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new RadarProcessingValidationResult(
            false,
            error,
            sourceId,
            eventIndex,
            message,
            metrics,
            expectedMetrics);
    }
}
