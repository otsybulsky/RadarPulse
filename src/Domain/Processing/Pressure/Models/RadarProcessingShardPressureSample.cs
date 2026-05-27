namespace RadarPulse.Domain.Processing;

/// <summary>
/// Pressure score and route metrics for one shard in a single sample.
/// </summary>
public readonly record struct RadarProcessingShardPressureSample
{
    /// <summary>
    /// Creates a shard pressure sample.
    /// </summary>
    public RadarProcessingShardPressureSample(
        int shardId,
        int partitionCount,
        int activePartitionCount,
        RadarProcessingRouteMetrics metrics,
        RadarProcessingPressureScore score,
        RadarProcessingPressureBand band)
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
        Score = score;
        Band = band;
    }

    /// <summary>
    /// Shard represented by the sample.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Number of partitions owned by the shard.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Number of owned partitions with work in the sample.
    /// </summary>
    public int ActivePartitionCount { get; }

    /// <summary>
    /// Route metrics observed for the shard.
    /// </summary>
    public RadarProcessingRouteMetrics Metrics { get; }

    /// <summary>
    /// Pressure score derived from metrics.
    /// </summary>
    public RadarProcessingPressureScore Score { get; }

    /// <summary>
    /// Band assigned to the pressure score.
    /// </summary>
    public RadarProcessingPressureBand Band { get; }

    /// <summary>
    /// Number of routed events in the sample.
    /// </summary>
    public long EventCount => Metrics.EventCount;

    /// <summary>
    /// Number of routed payload values in the sample.
    /// </summary>
    public long PayloadValueCount => Metrics.PayloadValueCount;

    /// <summary>
    /// Raw payload checksum observed for the shard.
    /// </summary>
    public long RawValueChecksum => Metrics.RawValueChecksum;

    /// <summary>
    /// Indicates whether the shard had routed work in the sample.
    /// </summary>
    public bool HasWork => EventCount > 0;
}
