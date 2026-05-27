namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingPartitionMigration
{
    public RadarProcessingPartitionMigration(
        long decisionId,
        RadarProcessingRebalanceMoveKind moveKind,
        RadarProcessingTopologyVersion expectedTopologyVersion,
        int partitionId,
        int sourceShardId,
        int targetShardId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decisionId);

        if (!Enum.IsDefined(moveKind) || moveKind == RadarProcessingRebalanceMoveKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(moveKind), moveKind, "Move kind must describe a real move.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceShardId);
        ArgumentOutOfRangeException.ThrowIfNegative(targetShardId);

        if (sourceShardId == targetShardId)
        {
            throw new ArgumentException("Source shard and target shard must be different.", nameof(targetShardId));
        }

        DecisionId = decisionId;
        MoveKind = moveKind;
        ExpectedTopologyVersion = expectedTopologyVersion;
        PartitionId = partitionId;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
    }

    public long DecisionId { get; }

    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    public RadarProcessingTopologyVersion ExpectedTopologyVersion { get; }

    public int PartitionId { get; }

    public int SourceShardId { get; }

    public int TargetShardId { get; }

    public RadarProcessingTopologyMoveRequest ToTopologyMoveRequest() =>
        new(
            ExpectedTopologyVersion,
            PartitionId,
            SourceShardId,
            TargetShardId);

    public static RadarProcessingPartitionMigration FromDecision(
        RadarProcessingRebalanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Candidate is null)
        {
            throw new ArgumentException("Migration decisions must carry a candidate.", nameof(decision));
        }

        return new RadarProcessingPartitionMigration(
            decision.DecisionId,
            decision.MoveKind,
            decision.TopologyVersion,
            decision.Candidate.PartitionId,
            decision.Candidate.SourceShardId,
            decision.Candidate.TargetShardId);
    }
}
