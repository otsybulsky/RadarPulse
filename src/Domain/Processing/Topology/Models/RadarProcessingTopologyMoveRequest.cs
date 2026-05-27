namespace RadarPulse.Domain.Processing;

/// <summary>
/// Optimistic request to change a partition owner in the processing topology.
/// </summary>
public readonly record struct RadarProcessingTopologyMoveRequest
{
    /// <summary>
    /// Creates a partition owner move request.
    /// </summary>
    public RadarProcessingTopologyMoveRequest(
        RadarProcessingTopologyVersion expectedTopologyVersion,
        int partitionId,
        int sourceShardId,
        int targetShardId)
    {
        ExpectedTopologyVersion = expectedTopologyVersion;
        PartitionId = partitionId;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
    }

    /// <summary>
    /// Topology version the caller based the move on.
    /// </summary>
    public RadarProcessingTopologyVersion ExpectedTopologyVersion { get; }

    /// <summary>
    /// Partition whose owner should change.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard that must currently own the partition.
    /// </summary>
    public int SourceShardId { get; }

    /// <summary>
    /// Shard that should own the partition after publication.
    /// </summary>
    public int TargetShardId { get; }
}
