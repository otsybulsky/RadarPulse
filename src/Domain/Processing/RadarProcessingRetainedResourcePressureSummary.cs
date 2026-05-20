namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedResourcePressureSummary
{
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

    public long CurrentPendingRetainedBatchCount { get; }

    public long CurrentPendingRetainedPayloadBytes { get; }

    public long PendingRetainedBatchCountHighWatermark { get; }

    public long PendingRetainedPayloadBytesHighWatermark { get; }

    public long CurrentActiveRetainedBatchCount { get; }

    public long CurrentActiveRetainedPayloadBytes { get; }

    public long ActiveRetainedBatchCountHighWatermark { get; }

    public long ActiveRetainedPayloadBytesHighWatermark { get; }

    public long CurrentCombinedRetainedBatchCount =>
        checked(CurrentPendingRetainedBatchCount + CurrentActiveRetainedBatchCount);

    public long CurrentCombinedRetainedPayloadBytes =>
        checked(CurrentPendingRetainedPayloadBytes + CurrentActiveRetainedPayloadBytes);

    public long CombinedRetainedBatchCountHighWatermark { get; }

    public long CombinedRetainedPayloadBytesHighWatermark { get; }

    public RadarProcessingRetainedResourcePressureSnapshot CurrentSnapshot =>
        new(
            CurrentPendingRetainedBatchCount,
            CurrentPendingRetainedPayloadBytes,
            CurrentActiveRetainedBatchCount,
            CurrentActiveRetainedPayloadBytes);

    public bool HasRetainedPressure =>
        CurrentCombinedRetainedBatchCount > 0 ||
        CombinedRetainedBatchCountHighWatermark > 0 ||
        CurrentCombinedRetainedPayloadBytes > 0 ||
        CombinedRetainedPayloadBytesHighWatermark > 0;

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
