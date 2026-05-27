namespace RadarPulse.Domain.Streaming;

public sealed record RadarEventBatchValidationResult(
    bool IsValid,
    RadarEventBatchValidationError Error,
    int EventIndex,
    string Message,
    RadarEventBatchMetrics Metrics,
    RadarEventBatchMetrics? ExpectedMetrics)
{
    public static RadarEventBatchValidationResult Valid(RadarEventBatchMetrics metrics) =>
        new(
            true,
            RadarEventBatchValidationError.None,
            EventIndex: -1,
            Message: string.Empty,
            metrics,
            ExpectedMetrics: null);

    public static RadarEventBatchValidationResult Invalid(
        RadarEventBatchValidationError error,
        int eventIndex,
        string message,
        RadarEventBatchMetrics metrics = default,
        RadarEventBatchMetrics? expectedMetrics = null)
    {
        if (error == RadarEventBatchValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new RadarEventBatchValidationResult(
            false,
            error,
            eventIndex,
            message,
            metrics,
            expectedMetrics);
    }
}
