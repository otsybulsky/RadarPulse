namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedResourcePressureSnapshot
{
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

    public long PendingBatchCount { get; }

    public long PendingPayloadBytes { get; }

    public long ActiveBatchCount { get; }

    public long ActivePayloadBytes { get; }

    public long CombinedBatchCount => checked(PendingBatchCount + ActiveBatchCount);

    public long CombinedPayloadBytes => checked(PendingPayloadBytes + ActivePayloadBytes);

    public static RadarProcessingRetainedResourcePressureSnapshot Empty { get; } = new();
}
