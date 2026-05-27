namespace RadarPulse.Domain.Processing;

public enum RadarProcessingStateHandoffValidationError
{
    None = 0,
    PartitionIdMismatch,
    SourceRangeMismatch,
    ActiveSourceCountMismatch,
    ProcessedEventCountMismatch,
    ProcessedPayloadValueCountMismatch,
    RawValueChecksumMismatch,
    ProcessingChecksumMismatch,
    LastMessageTimestampChecksumMismatch,
    HandlerSnapshotChecksumMismatch
}
