namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionPressureState
{
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

    public int PartitionId { get; }

    public int ShardId { get; }

    public int SampleCount { get; }

    public RadarProcessingRouteMetrics TotalMetrics { get; }

    public RadarProcessingPressureScore AverageScore { get; }

    public RadarProcessingPressureBand Band { get; }

    public bool IsHot => Band is RadarProcessingPressureBand.Hot or RadarProcessingPressureBand.SuperHot;

    public bool IsSuperHot => Band == RadarProcessingPressureBand.SuperHot;
}
