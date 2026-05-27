namespace RadarPulse.Domain.Processing;

/// <summary>
/// Migration command derived from an accepted rebalance decision.
/// </summary>
/// <remarks>
/// A migration carries the expected topology version and current owner shard so
/// publication can reject stale or ownership-mismatched decisions.
/// </remarks>
public sealed class RadarProcessingPartitionMigration
{
    /// <summary>
    /// Creates a partition migration command.
    /// </summary>
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

    /// <summary>
    /// Decision id that produced the migration.
    /// </summary>
    public long DecisionId { get; }

    /// <summary>
    /// Planner strategy that produced the move.
    /// </summary>
    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    /// <summary>
    /// Topology version that must still be current for publication.
    /// </summary>
    public RadarProcessingTopologyVersion ExpectedTopologyVersion { get; }

    /// <summary>
    /// Partition being migrated.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard that must currently own the partition.
    /// </summary>
    public int SourceShardId { get; }

    /// <summary>
    /// Target owner shard after publication.
    /// </summary>
    public int TargetShardId { get; }

    /// <summary>
    /// Converts the migration to a topology move request.
    /// </summary>
    public RadarProcessingTopologyMoveRequest ToTopologyMoveRequest() =>
        new(
            ExpectedTopologyVersion,
            PartitionId,
            SourceShardId,
            TargetShardId);

    /// <summary>
    /// Creates a migration from an accepted rebalance decision.
    /// </summary>
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
