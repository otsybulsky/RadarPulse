namespace RadarPulse.Domain.Processing;

/// <summary>
/// Carries validation details for invalid source-local order detected while constructing a batch delta.
/// </summary>
public sealed class RadarProcessingBatchDeltaValidationException : InvalidOperationException
{
    /// <summary>
    /// Creates a delta validation exception for a source and event index.
    /// </summary>
    public RadarProcessingBatchDeltaValidationException(
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message)
        : base(message)
    {
        Error = error;
        SourceId = sourceId;
        EventIndex = eventIndex;
    }

    /// <summary>
    /// Gets the validation error represented by the exception.
    /// </summary>
    public RadarProcessingValidationError Error { get; }

    /// <summary>
    /// Gets the source id associated with the failed delta event.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Gets the batch event index associated with the failed delta event.
    /// </summary>
    public int EventIndex { get; }
}
