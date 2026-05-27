namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingAsyncWorkItem
{
    private readonly IReadOnlyList<int> partitionIds;

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

    public long BatchSequence { get; }

    public int WorkItemId { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingWorkerId WorkerId { get; }

    public int ShardId { get; }

    public IReadOnlyList<int> PartitionIds => partitionIds;

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
