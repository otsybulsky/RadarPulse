namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingMetrics(
    long ProcessedBatchCount,
    long ProcessedStreamEventCount,
    long ProcessedPayloadValueCount,
    long ActiveSourceCount,
    long RawValueChecksum,
    ulong ProcessingChecksum)
{
    public static RadarProcessingMetrics Empty => default;
}
