namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingRetainedPayloadPrewarmResult(
    int EventCount,
    int PayloadBytes,
    int RetainedBatchCount,
    TimeSpan Elapsed,
    long AllocatedBytes,
    long EventPoolRetainedBytes,
    long PayloadPoolRetainedBytes);
