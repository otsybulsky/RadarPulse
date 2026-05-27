namespace RadarPulse.Domain.Processing;

/// <summary>
/// Processing telemetry for one routed shard.
/// </summary>
public readonly record struct RadarProcessingShardTelemetry
{
    /// <summary>
    /// Creates a shard telemetry sample.
    /// </summary>
    public RadarProcessingShardTelemetry(
        int shardId,
        int partitionCount,
        int activePartitionCount,
        RadarProcessingRouteMetrics metrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegative(activePartitionCount);

        if (activePartitionCount > partitionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activePartitionCount),
                activePartitionCount,
                "Active partition count must be less than or equal to partition count.");
        }

        ShardId = shardId;
        PartitionCount = partitionCount;
        ActivePartitionCount = activePartitionCount;
        Metrics = metrics;
    }

    /// <summary>
    /// Shard id reported by the telemetry.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Number of partitions owned by the shard in the topology snapshot.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Number of owned partitions that processed at least one event.
    /// </summary>
    public int ActivePartitionCount { get; }

    /// <summary>
    /// Route metrics observed for the shard.
    /// </summary>
    public RadarProcessingRouteMetrics Metrics { get; }

    /// <summary>
    /// Number of processed events.
    /// </summary>
    public long EventCount => Metrics.EventCount;

    /// <summary>
    /// Number of processed payload values.
    /// </summary>
    public long PayloadValueCount => Metrics.PayloadValueCount;

    /// <summary>
    /// Raw payload checksum observed for the shard.
    /// </summary>
    public long RawValueChecksum => Metrics.RawValueChecksum;

    /// <summary>
    /// Indicates whether the shard processed any events in the sample.
    /// </summary>
    public bool HasWork => EventCount > 0;
}
