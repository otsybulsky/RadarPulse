namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingValidationResult(
    bool IsValid,
    RadarProcessingValidationError Error,
    int SourceId,
    int EventIndex,
    string Message,
    RadarProcessingMetrics Metrics,
    RadarProcessingMetrics? ExpectedMetrics)
{
    public static RadarProcessingValidationResult Valid(RadarProcessingMetrics metrics) =>
        new(
            true,
            RadarProcessingValidationError.None,
            SourceId: -1,
            EventIndex: -1,
            Message: string.Empty,
            metrics,
            ExpectedMetrics: null);

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
