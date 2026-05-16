namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingShardBatchRoute
{
    private readonly int[] eventIndexes;

    internal RadarProcessingShardBatchRoute(
        int shardId,
        int[] eventIndexes,
        RadarProcessingRouteMetrics metrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentNullException.ThrowIfNull(eventIndexes);

        ShardId = shardId;
        this.eventIndexes = eventIndexes;
        Metrics = metrics;
    }

    public int ShardId { get; }

    public RadarProcessingRouteMetrics Metrics { get; }

    public ReadOnlyMemory<int> EventIndexes => eventIndexes;

    public bool HasWork => eventIndexes.Length > 0;
}
