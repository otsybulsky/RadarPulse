namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingShardPressureState
{
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

    public int ShardId { get; }

    public int SampleCount { get; }

    public int PartitionCount { get; }

    public int ActivePartitionCount { get; }

    public RadarProcessingRouteMetrics TotalMetrics { get; }

    public RadarProcessingPressureScore AverageScore { get; }

    public RadarProcessingPressureBand Band { get; }

    public bool IsHot => Band is RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot;

    public bool IsSuperHot => Band == RadarProcessingPressureBand.SuperHot;
}
