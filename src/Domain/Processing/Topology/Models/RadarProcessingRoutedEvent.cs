namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingRoutedEvent
{
    public RadarProcessingRoutedEvent(
        int eventIndex,
        int sourceId,
        int partitionId,
        int shardId,
        RadarProcessingPayloadMetrics payloadMetrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);

        EventIndex = eventIndex;
        SourceId = sourceId;
        PartitionId = partitionId;
        ShardId = shardId;
        PayloadMetrics = payloadMetrics;
    }

    public int EventIndex { get; }

    public int SourceId { get; }

    public int PartitionId { get; }

    public int ShardId { get; }

    public RadarProcessingPayloadMetrics PayloadMetrics { get; }
}
