namespace RadarPulse.Domain.Processing;

public readonly record struct RadarSourceProcessingSnapshot(
    int SourceId,
    bool IsActive,
    long ProcessedEventCount,
    long ProcessedPayloadValueCount,
    long RawValueChecksum,
    long LastMessageTimestampUtcTicks,
    ulong ProcessingChecksum);
