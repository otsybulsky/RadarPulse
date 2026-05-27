namespace RadarPulse.Domain.Processing;

/// <summary>
/// Current and high-water retained-resource pressure summary.
/// </summary>
public sealed record RadarProcessingRetainedResourcePressureSummary
{
    /// <summary>
    /// Creates a retained-resource pressure summary.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSummary(
        long currentPendingRetainedBatchCount = 0,
        long currentPendingRetainedPayloadBytes = 0,
        long pendingRetainedBatchCountHighWatermark = 0,
        long pendingRetainedPayloadBytesHighWatermark = 0,
        long currentActiveRetainedBatchCount = 0,
        long currentActiveRetainedPayloadBytes = 0,
        long activeRetainedBatchCountHighWatermark = 0,
        long activeRetainedPayloadBytesHighWatermark = 0,
        long combinedRetainedBatchCountHighWatermark = 0,
        long combinedRetainedPayloadBytesHighWatermark = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentPendingRetainedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(currentPendingRetainedPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingRetainedBatchCountHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingRetainedPayloadBytesHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(currentActiveRetainedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(currentActiveRetainedPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(activeRetainedBatchCountHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(activeRetainedPayloadBytesHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(combinedRetainedBatchCountHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(combinedRetainedPayloadBytesHighWatermark);

        EnsureHighWaterAtLeastCurrent(
            pendingRetainedBatchCountHighWatermark,
            currentPendingRetainedBatchCount,
            nameof(pendingRetainedBatchCountHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            pendingRetainedPayloadBytesHighWatermark,
            currentPendingRetainedPayloadBytes,
            nameof(pendingRetainedPayloadBytesHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            activeRetainedBatchCountHighWatermark,
            currentActiveRetainedBatchCount,
            nameof(activeRetainedBatchCountHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            activeRetainedPayloadBytesHighWatermark,
            currentActiveRetainedPayloadBytes,
            nameof(activeRetainedPayloadBytesHighWatermark));

        var currentCombinedRetainedBatchCount = checked(
            currentPendingRetainedBatchCount +
            currentActiveRetainedBatchCount);
        var currentCombinedRetainedPayloadBytes = checked(
            currentPendingRetainedPayloadBytes +
            currentActiveRetainedPayloadBytes);

        EnsureHighWaterAtLeastCurrent(
            combinedRetainedBatchCountHighWatermark,
            currentCombinedRetainedBatchCount,
            nameof(combinedRetainedBatchCountHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            combinedRetainedPayloadBytesHighWatermark,
            currentCombinedRetainedPayloadBytes,
            nameof(combinedRetainedPayloadBytesHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            combinedRetainedBatchCountHighWatermark,
            pendingRetainedBatchCountHighWatermark,
            nameof(combinedRetainedBatchCountHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            combinedRetainedBatchCountHighWatermark,
            activeRetainedBatchCountHighWatermark,
            nameof(combinedRetainedBatchCountHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            combinedRetainedPayloadBytesHighWatermark,
            pendingRetainedPayloadBytesHighWatermark,
            nameof(combinedRetainedPayloadBytesHighWatermark));
        EnsureHighWaterAtLeastCurrent(
            combinedRetainedPayloadBytesHighWatermark,
            activeRetainedPayloadBytesHighWatermark,
            nameof(combinedRetainedPayloadBytesHighWatermark));

        CurrentPendingRetainedBatchCount = currentPendingRetainedBatchCount;
        CurrentPendingRetainedPayloadBytes = currentPendingRetainedPayloadBytes;
        PendingRetainedBatchCountHighWatermark = pendingRetainedBatchCountHighWatermark;
        PendingRetainedPayloadBytesHighWatermark = pendingRetainedPayloadBytesHighWatermark;
        CurrentActiveRetainedBatchCount = currentActiveRetainedBatchCount;
        CurrentActiveRetainedPayloadBytes = currentActiveRetainedPayloadBytes;
        ActiveRetainedBatchCountHighWatermark = activeRetainedBatchCountHighWatermark;
        ActiveRetainedPayloadBytesHighWatermark = activeRetainedPayloadBytesHighWatermark;
        CombinedRetainedBatchCountHighWatermark = combinedRetainedBatchCountHighWatermark;
        CombinedRetainedPayloadBytesHighWatermark = combinedRetainedPayloadBytesHighWatermark;
    }

    /// <summary>
    /// Current queue-owned retained batch count.
    /// </summary>
    public long CurrentPendingRetainedBatchCount { get; }

    /// <summary>
    /// Current queue-owned retained payload bytes.
    /// </summary>
    public long CurrentPendingRetainedPayloadBytes { get; }

    /// <summary>
    /// Highest observed queue-owned retained batch count.
    /// </summary>
    public long PendingRetainedBatchCountHighWatermark { get; }

    /// <summary>
    /// Highest observed queue-owned retained payload bytes.
    /// </summary>
    public long PendingRetainedPayloadBytesHighWatermark { get; }

    /// <summary>
    /// Current consumer-owned retained batch count.
    /// </summary>
    public long CurrentActiveRetainedBatchCount { get; }

    /// <summary>
    /// Current consumer-owned retained payload bytes.
    /// </summary>
    public long CurrentActiveRetainedPayloadBytes { get; }

    /// <summary>
    /// Highest observed consumer-owned retained batch count.
    /// </summary>
    public long ActiveRetainedBatchCountHighWatermark { get; }

    /// <summary>
    /// Highest observed consumer-owned retained payload bytes.
    /// </summary>
    public long ActiveRetainedPayloadBytesHighWatermark { get; }

    /// <summary>
    /// Current pending plus active retained batch count.
    /// </summary>
    public long CurrentCombinedRetainedBatchCount =>
        checked(CurrentPendingRetainedBatchCount + CurrentActiveRetainedBatchCount);

    /// <summary>
    /// Current pending plus active retained payload bytes.
    /// </summary>
    public long CurrentCombinedRetainedPayloadBytes =>
        checked(CurrentPendingRetainedPayloadBytes + CurrentActiveRetainedPayloadBytes);

    /// <summary>
    /// Highest observed combined retained batch count.
    /// </summary>
    public long CombinedRetainedBatchCountHighWatermark { get; }

    /// <summary>
    /// Highest observed combined retained payload bytes.
    /// </summary>
    public long CombinedRetainedPayloadBytesHighWatermark { get; }

    /// <summary>
    /// Snapshot of current pending and active pressure.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSnapshot CurrentSnapshot =>
        new(
            CurrentPendingRetainedBatchCount,
            CurrentPendingRetainedPayloadBytes,
            CurrentActiveRetainedBatchCount,
            CurrentActiveRetainedPayloadBytes);

    /// <summary>
    /// Indicates whether current or historical retained-resource pressure exists.
    /// </summary>
    public bool HasRetainedPressure =>
        CurrentCombinedRetainedBatchCount > 0 ||
        CombinedRetainedBatchCountHighWatermark > 0 ||
        CurrentCombinedRetainedPayloadBytes > 0 ||
        CombinedRetainedPayloadBytesHighWatermark > 0;

    /// <summary>
    /// Empty retained-resource pressure summary.
    /// </summary>
    public static RadarProcessingRetainedResourcePressureSummary Empty { get; } = new();

    internal static RadarProcessingRetainedResourcePressureSummary FromState(
        RadarProcessingRetainedResourcePressureSnapshot current,
        long pendingBatchCountHighWatermark,
        long pendingPayloadBytesHighWatermark,
        long activeBatchCountHighWatermark,
        long activePayloadBytesHighWatermark,
        long combinedBatchCountHighWatermark,
        long combinedPayloadBytesHighWatermark)
    {
        ArgumentNullException.ThrowIfNull(current);

        return new RadarProcessingRetainedResourcePressureSummary(
            current.PendingBatchCount,
            current.PendingPayloadBytes,
            pendingBatchCountHighWatermark,
            pendingPayloadBytesHighWatermark,
            current.ActiveBatchCount,
            current.ActivePayloadBytes,
            activeBatchCountHighWatermark,
            activePayloadBytesHighWatermark,
            combinedBatchCountHighWatermark,
            combinedPayloadBytesHighWatermark);
    }

    private static void EnsureHighWaterAtLeastCurrent(
        long highWatermark,
        long current,
        string paramName)
    {
        if (highWatermark < current)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                highWatermark,
                "High-water mark cannot be less than the current value.");
        }
    }
}
