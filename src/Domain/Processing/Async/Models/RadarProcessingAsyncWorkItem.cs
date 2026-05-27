namespace RadarPulse.Domain.Processing;

/// <summary>
/// Describes one shard-scoped unit of async processing work for a batch route.
/// </summary>
/// <remarks>
/// Partition ids are copied, must be non-empty, ordered, and unique, and are interpreted inside
/// the captured <see cref="TopologyVersion"/>.
/// </remarks>
public sealed record RadarProcessingAsyncWorkItem
{
    private readonly IReadOnlyList<int> partitionIds;

    /// <summary>
    /// Creates an async work item for a worker, shard, and ordered partition set.
    /// </summary>
    public RadarProcessingAsyncWorkItem(
        long batchSequence,
        int workItemId,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingWorkerId workerId,
        int shardId,
        IReadOnlyCollection<int> partitionIds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(batchSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(workItemId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentNullException.ThrowIfNull(partitionIds);

        BatchSequence = batchSequence;
        WorkItemId = workItemId;
        TopologyVersion = topologyVersion;
        WorkerId = workerId;
        ShardId = shardId;
        this.partitionIds = CopyPartitionIds(partitionIds);
    }

    /// <summary>
    /// Gets the batch sequence that owns the work item.
    /// </summary>
    public long BatchSequence { get; }

    /// <summary>
    /// Gets the zero-based work item id within the batch scope.
    /// </summary>
    public int WorkItemId { get; }

    /// <summary>
    /// Gets the topology version captured when the route was created.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Gets the worker assigned to execute the work item.
    /// </summary>
    public RadarProcessingWorkerId WorkerId { get; }

    /// <summary>
    /// Gets the shard whose routed events are covered by the work item.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Gets ordered partition ids owned by the work item.
    /// </summary>
    public IReadOnlyList<int> PartitionIds => partitionIds;

    /// <summary>
    /// Gets the number of partitions owned by the work item.
    /// </summary>
    public int PartitionCount => partitionIds.Count;

    private static IReadOnlyList<int> CopyPartitionIds(
        IReadOnlyCollection<int> partitionIds)
    {
        if (partitionIds.Count == 0)
        {
            throw new ArgumentException("Work item must contain at least one partition id.", nameof(partitionIds));
        }

        var result = partitionIds.ToArray();
        var previous = -1;
        for (var i = 0; i < result.Length; i++)
        {
            var partitionId = result[i];
            ArgumentOutOfRangeException.ThrowIfNegative(partitionId, nameof(partitionIds));
            if (partitionId <= previous)
            {
                throw new ArgumentException(
                    "Work item partition ids must be ordered and unique.",
                    nameof(partitionIds));
            }

            previous = partitionId;
        }

        return Array.AsReadOnly(result);
    }
}
