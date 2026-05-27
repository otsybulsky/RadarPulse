namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionTelemetry
{
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

    public int PartitionId { get; }

    public int ShardId { get; }

    public RadarProcessingRouteMetrics Metrics { get; }

    public long EventCount => Metrics.EventCount;

    public long PayloadValueCount => Metrics.PayloadValueCount;

    public long RawValueChecksum => Metrics.RawValueChecksum;

    public bool HasWork => EventCount > 0;
}
