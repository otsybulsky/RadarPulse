namespace RadarPulse.Domain.Processing;

/// <summary>
/// Processing telemetry for one routed partition.
/// </summary>
public readonly record struct RadarProcessingPartitionTelemetry
{
    /// <summary>
    /// Creates a partition telemetry sample.
    /// </summary>
    public RadarProcessingPartitionTelemetry(
        int partitionId,
        int shardId,
        RadarProcessingRouteMetrics metrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);

        PartitionId = partitionId;
        ShardId = shardId;
        Metrics = metrics;
    }

    /// <summary>
    /// Partition id reported by the telemetry.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard that owned the partition when telemetry was produced.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Route metrics observed for the partition.
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
    /// Raw payload checksum observed for the partition.
    /// </summary>
    public long RawValueChecksum => Metrics.RawValueChecksum;

    /// <summary>
    /// Indicates whether the partition processed any events in the sample.
    /// </summary>
    public bool HasWork => EventCount > 0;
}
