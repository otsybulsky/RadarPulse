namespace RadarPulse.Domain.Processing;

/// <summary>
/// Rolling pressure state for one shard across the pressure window.
/// </summary>
public readonly record struct RadarProcessingShardPressureState
{
    /// <summary>
    /// Creates a rolling shard pressure state.
    /// </summary>
    public RadarProcessingShardPressureState(
        int shardId,
        int sampleCount,
        int partitionCount,
        int activePartitionCount,
        RadarProcessingRouteMetrics totalMetrics,
        RadarProcessingPressureScore averageScore,
        RadarProcessingPressureBand band)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);
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
        SampleCount = sampleCount;
        PartitionCount = partitionCount;
        ActivePartitionCount = activePartitionCount;
        TotalMetrics = totalMetrics;
        AverageScore = averageScore;
        Band = band;
    }

    /// <summary>
    /// Shard represented by the state.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Number of samples included in the rolling state.
    /// </summary>
    public int SampleCount { get; }

    /// <summary>
    /// Number of partitions owned by the shard in the latest sample.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Number of owned partitions with work in the latest sample.
    /// </summary>
    public int ActivePartitionCount { get; }

    /// <summary>
    /// Metrics accumulated across retained samples.
    /// </summary>
    public RadarProcessingRouteMetrics TotalMetrics { get; }

    /// <summary>
    /// Average score across retained samples.
    /// </summary>
    public RadarProcessingPressureScore AverageScore { get; }

    /// <summary>
    /// Hysteresis-adjusted pressure band.
    /// </summary>
    public RadarProcessingPressureBand Band { get; }

    /// <summary>
    /// Indicates whether the shard is currently hot or super-hot.
    /// </summary>
    public bool IsHot => Band is RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot;

    /// <summary>
    /// Indicates whether the shard is currently super-hot.
    /// </summary>
    public bool IsSuperHot => Band == RadarProcessingPressureBand.SuperHot;
}
