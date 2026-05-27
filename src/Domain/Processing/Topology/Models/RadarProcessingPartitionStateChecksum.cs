namespace RadarPulse.Domain.Processing;

/// <summary>
/// Deterministic checksums used to verify partition state handoff.
/// </summary>
/// <remarks>
/// The three checksum lanes keep processing totals, last-message timestamps, and
/// handler snapshots independently comparable so a failed handoff can identify
/// the mismatched state category.
/// </remarks>
public readonly record struct RadarProcessingPartitionStateChecksum(
    /// <summary>
    /// Checksum over source processing counters and processing checksum values.
    /// </summary>
    ulong ProcessingChecksum,

    /// <summary>
    /// Checksum over source last-message timestamps.
    /// </summary>
    ulong LastMessageTimestampChecksum,

    /// <summary>
    /// Checksum over handler snapshot fields for active sources.
    /// </summary>
    ulong HandlerSnapshotChecksum)
{
    /// <summary>
    /// Empty checksum used when a partition has no active source state.
    /// </summary>
    public static RadarProcessingPartitionStateChecksum Empty => default;
}
