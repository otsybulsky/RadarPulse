namespace RadarPulse.Domain.Processing;

/// <summary>
/// Routed work and metrics for one shard inside a batch route.
/// </summary>
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

    /// <summary>
    /// Shard represented by the route.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Payload metrics accumulated for all events routed to the shard.
    /// </summary>
    public RadarProcessingRouteMetrics Metrics { get; }

    /// <summary>
    /// Original batch event indexes assigned to the shard.
    /// </summary>
    public ReadOnlyMemory<int> EventIndexes => eventIndexes;

    /// <summary>
    /// Indicates whether the shard has events in this batch.
    /// </summary>
    public bool HasWork => eventIndexes.Length > 0;
}
