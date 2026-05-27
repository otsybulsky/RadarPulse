namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingRetainedPayloadPrewarmResult(
    int EventCount,
    int PayloadBytes,
    int RetainedBatchCount,
    TimeSpan Elapsed,
    long AllocatedBytes,
    long EventPoolRetainedBytes,
    long PayloadPoolRetainedBytes)
{
    public static RadarProcessingRetainedPayloadPrewarmResult None { get; } =
        new(0, 0, 0, TimeSpan.Zero, 0, 0, 0);

    public bool Applied => RetainedBatchCount > 0;

    public long RetainedBytes => EventPoolRetainedBytes + PayloadPoolRetainedBytes;
}
