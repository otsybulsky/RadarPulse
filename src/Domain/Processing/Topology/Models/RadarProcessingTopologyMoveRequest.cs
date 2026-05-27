namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingTopologyMoveRequest
{
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

    public RadarProcessingTopologyVersion ExpectedTopologyVersion { get; }

    public int PartitionId { get; }

    public int SourceShardId { get; }

    public int TargetShardId { get; }
}
