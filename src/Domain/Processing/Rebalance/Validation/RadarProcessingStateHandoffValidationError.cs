namespace RadarPulse.Domain.Processing;

/// <summary>
/// Mismatch categories for partition state handoff validation.
/// </summary>
public enum RadarProcessingStateHandoffValidationError
{
    /// <summary>
    /// No handoff validation error.
    /// </summary>
    None = 0,

    /// <summary>
    /// Before and after snapshots describe different partitions.
    /// </summary>
    PartitionIdMismatch,

    /// <summary>
    /// Before and after snapshots cover different source-id ranges.
    /// </summary>
    SourceRangeMismatch,

    /// <summary>
    /// Active source counts differ.
    /// </summary>
    ActiveSourceCountMismatch,

    /// <summary>
    /// Processed event totals differ.
    /// </summary>
    ProcessedEventCountMismatch,

    /// <summary>
    /// Processed payload value totals differ.
    /// </summary>
    ProcessedPayloadValueCountMismatch,

    /// <summary>
    /// Raw payload checksums differ.
    /// </summary>
    RawValueChecksumMismatch,

    /// <summary>
    /// Processing state checksums differ.
    /// </summary>
    ProcessingChecksumMismatch,

    /// <summary>
    /// Last-message timestamp checksums differ.
    /// </summary>
    LastMessageTimestampChecksumMismatch,

    /// <summary>
    /// Handler snapshot checksums differ.
    /// </summary>
    HandlerSnapshotChecksumMismatch
}
