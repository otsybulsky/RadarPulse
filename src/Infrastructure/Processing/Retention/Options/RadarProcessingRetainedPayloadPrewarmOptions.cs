namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingRetainedPayloadPrewarmOptions
{
    public static RadarProcessingRetainedPayloadPrewarmOptions None { get; } = new(0, 0, 0);

    public static RadarProcessingRetainedPayloadPrewarmOptions RolloutDefault { get; } =
        new(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount);

    public RadarProcessingRetainedPayloadPrewarmOptions(
        int eventCount,
        int payloadBytes,
        int retainedBatchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedBatchCount);

        var anyEnabledValue = eventCount > 0 || payloadBytes > 0 || retainedBatchCount > 0;
        var allEnabledValues = eventCount > 0 && payloadBytes > 0 && retainedBatchCount > 0;
        if (anyEnabledValue && !allEnabledValues)
        {
            throw new ArgumentException(
                "Retained payload prewarm requires positive event count, payload bytes, and retained batch count.");
        }

        EventCount = eventCount;
        PayloadBytes = payloadBytes;
        RetainedBatchCount = retainedBatchCount;
    }

    public int EventCount { get; }

    public int PayloadBytes { get; }

    public int RetainedBatchCount { get; }

    public bool Enabled => RetainedBatchCount > 0;
}
