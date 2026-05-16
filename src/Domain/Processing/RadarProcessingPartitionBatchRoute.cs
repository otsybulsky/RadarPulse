namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingPartitionBatchRoute
{
    private readonly int[] eventIndexes;

    internal RadarProcessingPartitionBatchRoute(
        int partitionId,
        int shardId,
        int[] eventIndexes,
        RadarProcessingRouteMetrics metrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentNullException.ThrowIfNull(eventIndexes);

        PartitionId = partitionId;
        ShardId = shardId;
        this.eventIndexes = eventIndexes;
        Metrics = metrics;
    }

    public int PartitionId { get; }

    public int ShardId { get; }

    public RadarProcessingRouteMetrics Metrics { get; }

    public ReadOnlyMemory<int> EventIndexes => eventIndexes;

    public bool HasWork => eventIndexes.Length > 0;
}
