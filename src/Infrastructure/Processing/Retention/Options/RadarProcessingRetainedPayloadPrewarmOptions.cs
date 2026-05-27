namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Prewarm settings for retained payload event and byte array pools.
/// </summary>
public sealed record RadarProcessingRetainedPayloadPrewarmOptions
{
    /// <summary>
    /// Disabled prewarm settings.
    /// </summary>
    public static RadarProcessingRetainedPayloadPrewarmOptions None { get; } = new(0, 0, 0);

    /// <summary>
    /// Rollout default prewarm settings used by archive rebalance benchmark paths.
    /// </summary>
    public static RadarProcessingRetainedPayloadPrewarmOptions RolloutDefault { get; } =
        new(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount);

    /// <summary>
    /// Creates prewarm settings; all dimensions must be positive when prewarm is enabled.
    /// </summary>
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

    /// <summary>
    /// Event array length to prewarm.
    /// </summary>
    public int EventCount { get; }

    /// <summary>
    /// Payload byte array length to prewarm.
    /// </summary>
    public int PayloadBytes { get; }

    /// <summary>
    /// Number of retained batch-sized arrays to seed in each pool.
    /// </summary>
    public int RetainedBatchCount { get; }

    /// <summary>
    /// Indicates whether prewarm should allocate retained arrays.
    /// </summary>
    public bool Enabled => RetainedBatchCount > 0;
}
