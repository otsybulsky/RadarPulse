namespace RadarPulse.Domain.Processing;

/// <summary>
/// Rolling pressure state for one partition across the pressure window.
/// </summary>
public readonly record struct RadarProcessingPartitionPressureState
{
    /// <summary>
    /// Creates a rolling partition pressure state.
    /// </summary>
    public RadarProcessingPartitionPressureState(
        int partitionId,
        int shardId,
        int sampleCount,
        RadarProcessingRouteMetrics totalMetrics,
        RadarProcessingPressureScore averageScore,
        RadarProcessingPressureBand band)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);

        PartitionId = partitionId;
        ShardId = shardId;
        SampleCount = sampleCount;
        TotalMetrics = totalMetrics;
        AverageScore = averageScore;
        Band = band;
    }

    /// <summary>
    /// Partition represented by the state.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Owner shard from the latest sample.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Number of samples included in the rolling state.
    /// </summary>
    public int SampleCount { get; }

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
    /// Indicates whether the partition is currently hot or super-hot.
    /// </summary>
    public bool IsHot => Band is RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot;

    /// <summary>
    /// Indicates whether the partition is currently super-hot.
    /// </summary>
    public bool IsSuperHot => Band == RadarProcessingPressureBand.SuperHot;
}
