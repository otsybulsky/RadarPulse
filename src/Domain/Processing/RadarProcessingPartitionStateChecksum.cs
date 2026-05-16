namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionStateChecksum(
    ulong ProcessingChecksum,
    ulong LastMessageTimestampChecksum,
    ulong HandlerSnapshotChecksum)
{
    public static RadarProcessingPartitionStateChecksum Empty => default;
}
