namespace RadarPulse.Domain.Processing;

/// <summary>
/// Summarizes allocation cost for retained owned snapshots captured by provider queue telemetry.
/// </summary>
public sealed record RadarProcessingOwnedSnapshotAllocationSummary
{
    /// <summary>
    /// Gets an empty owned snapshot allocation summary.
    /// </summary>
    public static RadarProcessingOwnedSnapshotAllocationSummary Empty { get; } = new();

    /// <summary>
    /// Creates an owned snapshot allocation summary and validates counters and elapsed time.
    /// </summary>
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

    /// <summary>
    /// Gets the number of owned snapshots captured.
    /// </summary>
    public long SnapshotCount { get; }

    /// <summary>
    /// Gets retained payload bytes represented by owned snapshots.
    /// </summary>
    public long PayloadBytes { get; }

    /// <summary>
    /// Gets retained payload value count represented by owned snapshots.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Gets allocated bytes attributed to owned snapshot capture.
    /// </summary>
    public long AllocatedBytes { get; }

    /// <summary>
    /// Gets elapsed time spent capturing owned snapshots.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets allocated bytes per owned snapshot.
    /// </summary>
    public double AllocatedBytesPerSnapshot =>
        SnapshotCount == 0 ? 0.0 : (double)AllocatedBytes / SnapshotCount;

    /// <summary>
    /// Gets allocated bytes per retained payload byte.
    /// </summary>
    public double AllocatedBytesPerPayloadByte =>
        PayloadBytes == 0 ? 0.0 : (double)AllocatedBytes / PayloadBytes;

    /// <summary>
    /// Gets allocated bytes per retained payload value.
    /// </summary>
    public double AllocatedBytesPerPayloadValue =>
        PayloadValueCount == 0 ? 0.0 : (double)AllocatedBytes / PayloadValueCount;

    /// <summary>
    /// Gets retained payload bytes per owned snapshot.
    /// </summary>
    public double PayloadBytesPerSnapshot =>
        SnapshotCount == 0 ? 0.0 : (double)PayloadBytes / SnapshotCount;

    /// <summary>
    /// Creates an allocation summary from provider queue telemetry.
    /// </summary>
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
