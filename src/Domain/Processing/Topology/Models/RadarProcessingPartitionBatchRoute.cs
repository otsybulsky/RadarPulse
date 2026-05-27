namespace RadarPulse.Domain.Processing;

/// <summary>
/// Routed work and metrics for one partition inside a batch route.
/// </summary>
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

    /// <summary>
    /// Partition represented by the route.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard that owned the partition when the route was created.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Payload metrics accumulated for this partition.
    /// </summary>
    public RadarProcessingRouteMetrics Metrics { get; }

    /// <summary>
    /// Original batch event indexes assigned to the partition.
    /// </summary>
    public ReadOnlyMemory<int> EventIndexes => eventIndexes;

    /// <summary>
    /// Indicates whether the partition has events in this batch.
    /// </summary>
    public bool HasWork => eventIndexes.Length > 0;
}
