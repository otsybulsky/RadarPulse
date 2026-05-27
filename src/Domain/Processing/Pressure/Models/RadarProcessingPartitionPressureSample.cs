namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionPressureSample
{
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

    public int PartitionId { get; }

    public int ShardId { get; }

    public RadarProcessingRouteMetrics Metrics { get; }

    public RadarProcessingPressureScore Score { get; }

    public RadarProcessingPressureBand Band { get; }

    public long EventCount => Metrics.EventCount;

    public long PayloadValueCount => Metrics.PayloadValueCount;

    public long RawValueChecksum => Metrics.RawValueChecksum;

    public bool HasWork => EventCount > 0;
}
