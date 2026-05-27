namespace RadarPulse.Domain.Processing;

/// <summary>
/// Processing snapshot for one source after committed event application.
/// </summary>
public readonly record struct RadarSourceProcessingSnapshot(
    int SourceId,
    bool IsActive,
    long ProcessedEventCount,
    long ProcessedPayloadValueCount,
    long RawValueChecksum,
    long LastMessageTimestampUtcTicks,
    ulong ProcessingChecksum);
