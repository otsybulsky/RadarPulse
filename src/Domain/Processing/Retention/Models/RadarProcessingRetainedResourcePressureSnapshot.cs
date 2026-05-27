namespace RadarPulse.Domain.Processing;

/// <summary>
/// Current pending and active retained-resource pressure.
/// </summary>
public sealed record RadarProcessingRetainedResourcePressureSnapshot
{
    /// <summary>
    /// Creates a retained-resource pressure snapshot.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSnapshot(
        long pendingBatchCount = 0,
        long pendingPayloadBytes = 0,
        long activeBatchCount = 0,
        long activePayloadBytes = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pendingBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(activeBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(activePayloadBytes);

        PendingBatchCount = pendingBatchCount;
        PendingPayloadBytes = pendingPayloadBytes;
        ActiveBatchCount = activeBatchCount;
        ActivePayloadBytes = activePayloadBytes;
    }

    /// <summary>
    /// Queue-owned retained batch count.
    /// </summary>
    public long PendingBatchCount { get; }

    /// <summary>
    /// Queue-owned retained payload bytes.
    /// </summary>
    public long PendingPayloadBytes { get; }

    /// <summary>
    /// Consumer-owned retained batch count.
    /// </summary>
    public long ActiveBatchCount { get; }

    /// <summary>
    /// Consumer-owned retained payload bytes.
    /// </summary>
    public long ActivePayloadBytes { get; }

    /// <summary>
    /// Combined pending and active retained batch count.
    /// </summary>
    public long CombinedBatchCount => checked(PendingBatchCount + ActiveBatchCount);

    /// <summary>
    /// Combined pending and active retained payload bytes.
    /// </summary>
    public long CombinedPayloadBytes => checked(PendingPayloadBytes + ActivePayloadBytes);

    /// <summary>
    /// Empty retained-resource pressure snapshot.
    /// </summary>
    public static RadarProcessingRetainedResourcePressureSnapshot Empty { get; } = new();
}
