namespace RadarPulse.Domain.Processing;

internal sealed class RadarProcessingBatchDeltaValidationException : InvalidOperationException
{
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

    public RadarProcessingValidationError Error { get; }

    public int SourceId { get; }

    public int EventIndex { get; }
}
