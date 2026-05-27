namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Result of validating a radar event batch.
/// </summary>
public sealed record RadarEventBatchValidationResult(
    /// <summary>
    /// Indicates whether the batch passed validation.
    /// </summary>
    bool IsValid,

    /// <summary>
    /// Validation error, or none when valid.
    /// </summary>
    RadarEventBatchValidationError Error,

    /// <summary>
    /// Event index associated with the failure, or -1 for batch-level errors.
    /// </summary>
    int EventIndex,

    /// <summary>
    /// Diagnostic validation message.
    /// </summary>
    string Message,

    /// <summary>
    /// Computed batch metrics when available.
    /// </summary>
    RadarEventBatchMetrics Metrics,

    /// <summary>
    /// Expected metrics supplied by the caller for metrics mismatch diagnostics.
    /// </summary>
    RadarEventBatchMetrics? ExpectedMetrics)
{
    /// <summary>
    /// Creates a valid validation result with computed metrics.
    /// </summary>
    public static RadarEventBatchValidationResult Valid(RadarEventBatchMetrics metrics) =>
        new(
            true,
            RadarEventBatchValidationError.None,
            EventIndex: -1,
            Message: string.Empty,
            metrics,
            ExpectedMetrics: null);

    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
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
