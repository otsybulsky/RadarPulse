namespace RadarPulse.Domain.Processing;

/// <summary>
/// Ownership and payload metrics for one event in a routed batch.
/// </summary>
public readonly record struct RadarProcessingRoutedEvent
{
    /// <summary>
    /// Creates a routed event entry for an original batch event index.
    /// </summary>
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

    /// <summary>
    /// Original event index in the routed batch.
    /// </summary>
    public int EventIndex { get; }

    /// <summary>
    /// Source id carried by the event.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Partition selected for the source id.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Owner shard selected for the partition.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Payload metrics computed for this event.
    /// </summary>
    public RadarProcessingPayloadMetrics PayloadMetrics { get; }
}
