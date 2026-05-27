namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Evidence captured while prewarming retained payload pools.
/// </summary>
public sealed record RadarProcessingRetainedPayloadPrewarmResult(
    /// <summary>
    /// Event array length requested for prewarm.
    /// </summary>
    int EventCount,
    /// <summary>
    /// Payload byte array length requested for prewarm.
    /// </summary>
    int PayloadBytes,
    /// <summary>
    /// Number of batch-sized arrays requested for retention.
    /// </summary>
    int RetainedBatchCount,
    /// <summary>
    /// Elapsed prewarm time.
    /// </summary>
    TimeSpan Elapsed,
    /// <summary>
    /// Current-thread allocation delta observed during prewarm.
    /// </summary>
    long AllocatedBytes,
    /// <summary>
    /// Bytes retained by the event array pool after prewarm.
    /// </summary>
    long EventPoolRetainedBytes,
    /// <summary>
    /// Bytes retained by the payload byte array pool after prewarm.
    /// </summary>
    long PayloadPoolRetainedBytes)
{
    /// <summary>
    /// Result used when prewarm is disabled.
    /// </summary>
    public static RadarProcessingRetainedPayloadPrewarmResult None { get; } =
        new(0, 0, 0, TimeSpan.Zero, 0, 0, 0);

    /// <summary>
    /// Indicates whether prewarm retained any batch-sized arrays.
    /// </summary>
    public bool Applied => RetainedBatchCount > 0;

    /// <summary>
    /// Total retained bytes across event and payload pools.
    /// </summary>
    public long RetainedBytes => EventPoolRetainedBytes + PayloadPoolRetainedBytes;
}
