namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingOwnedSnapshotAllocationSummary
{
    public static RadarProcessingOwnedSnapshotAllocationSummary Empty { get; } = new();

    public RadarProcessingOwnedSnapshotAllocationSummary(
        long snapshotCount = 0,
        long payloadBytes = 0,
        long payloadValueCount = 0,
        long allocatedBytes = 0,
        TimeSpan elapsed = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(snapshotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(allocatedBytes);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        SnapshotCount = snapshotCount;
        PayloadBytes = payloadBytes;
        PayloadValueCount = payloadValueCount;
        AllocatedBytes = allocatedBytes;
        Elapsed = elapsed;
    }

    public long SnapshotCount { get; }

    public long PayloadBytes { get; }

    public long PayloadValueCount { get; }

    public long AllocatedBytes { get; }

    public TimeSpan Elapsed { get; }

    public double AllocatedBytesPerSnapshot =>
        SnapshotCount == 0 ? 0.0 : (double)AllocatedBytes / SnapshotCount;

    public double AllocatedBytesPerPayloadByte =>
        PayloadBytes == 0 ? 0.0 : (double)AllocatedBytes / PayloadBytes;

    public double AllocatedBytesPerPayloadValue =>
        PayloadValueCount == 0 ? 0.0 : (double)AllocatedBytes / PayloadValueCount;

    public double PayloadBytesPerSnapshot =>
        SnapshotCount == 0 ? 0.0 : (double)PayloadBytes / SnapshotCount;

    public static RadarProcessingOwnedSnapshotAllocationSummary FromTelemetry(
        RadarProcessingProviderQueueTelemetrySummary telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        return new RadarProcessingOwnedSnapshotAllocationSummary(
            telemetry.OwnedSnapshotCount,
            telemetry.OwnedSnapshotPayloadBytes,
            telemetry.OwnedSnapshotPayloadValueCount,
            telemetry.OwnedSnapshotAllocatedBytes,
            telemetry.TotalOwnedSnapshotTime);
    }
}
