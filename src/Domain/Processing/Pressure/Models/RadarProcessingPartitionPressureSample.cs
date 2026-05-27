namespace RadarPulse.Domain.Processing;

/// <summary>
/// Pressure score and route metrics for one partition in a single sample.
/// </summary>
public readonly record struct RadarProcessingPartitionPressureSample
{
    /// <summary>
    /// Creates a partition pressure sample.
    /// </summary>
    public RadarProcessingPartitionPressureSample(
        int partitionId,
        int shardId,
        RadarProcessingRouteMetrics metrics,
        RadarProcessingPressureScore score,
        RadarProcessingPressureBand band)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);

        PartitionId = partitionId;
        ShardId = shardId;
        Metrics = metrics;
        Score = score;
        Band = band;
    }

    /// <summary>
    /// Partition represented by the sample.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Owner shard at the sampled topology version.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Route metrics observed for the partition.
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
    /// Raw payload checksum observed for the partition.
    /// </summary>
    public long RawValueChecksum => Metrics.RawValueChecksum;

    /// <summary>
    /// Indicates whether the partition had routed work in the sample.
    /// </summary>
    public bool HasWork => EventCount > 0;
}
