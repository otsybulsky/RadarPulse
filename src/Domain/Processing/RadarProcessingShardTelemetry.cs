namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingShardTelemetry
{
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

    public int ShardId { get; }

    public int PartitionCount { get; }

    public int ActivePartitionCount { get; }

    public RadarProcessingRouteMetrics Metrics { get; }

    public long EventCount => Metrics.EventCount;

    public long PayloadValueCount => Metrics.PayloadValueCount;

    public long RawValueChecksum => Metrics.RawValueChecksum;

    public bool HasWork => EventCount > 0;
}
