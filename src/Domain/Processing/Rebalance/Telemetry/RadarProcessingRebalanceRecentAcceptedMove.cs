namespace RadarPulse.Domain.Processing;

/// <summary>
/// Retained compact detail for a recently accepted rebalance move.
/// </summary>
public sealed record RadarProcessingRebalanceRecentAcceptedMove
{
    /// <summary>
    /// Creates a retained accepted-move detail entry.
    /// </summary>
    public RadarProcessingRebalanceRecentAcceptedMove(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingTopologyVersion? resultTopologyVersion,
        RadarProcessingRebalanceMoveKind moveKind,
        int partitionId,
        int sourceShardId,
        int targetShardId,
        RadarProcessingProjectedPressure projectedPressure,
        double expectedRelief)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decisionId);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);

        if (moveKind is not RadarProcessingRebalanceMoveKind.DirectHotRelief and
            not RadarProcessingRebalanceMoveKind.ColdEvacuation and
            not RadarProcessingRebalanceMoveKind.RoomMakingReserved)
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

        if (double.IsNaN(expectedRelief) || double.IsInfinity(expectedRelief) || expectedRelief < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedRelief),
                expectedRelief,
                "Expected relief must be finite and non-negative.");
        }

        DecisionId = decisionId;
        EvaluationSequence = evaluationSequence;
        TopologyVersion = topologyVersion;
        ResultTopologyVersion = resultTopologyVersion;
        MoveKind = moveKind;
        PartitionId = partitionId;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
        ProjectedPressure = projectedPressure;
        ExpectedRelief = expectedRelief;
    }

    /// <summary>
    /// Decision id that accepted the move.
    /// </summary>
    public long DecisionId { get; }

    /// <summary>
    /// Policy evaluation sequence for the decision.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version evaluated before publication.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Topology version produced by the publication, when known.
    /// </summary>
    public RadarProcessingTopologyVersion? ResultTopologyVersion { get; }

    /// <summary>
    /// Strategy that produced the accepted move.
    /// </summary>
    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    /// <summary>
    /// Partition that moved.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Source owner shard before the move.
    /// </summary>
    public int SourceShardId { get; }

    /// <summary>
    /// Target owner shard after the move.
    /// </summary>
    public int TargetShardId { get; }

    /// <summary>
    /// Projected pressure used when the move was accepted.
    /// </summary>
    public RadarProcessingProjectedPressure ProjectedPressure { get; }

    /// <summary>
    /// Expected pressure relief used when the move was accepted.
    /// </summary>
    public double ExpectedRelief { get; }

    /// <summary>
    /// Creates retained accepted-move detail from an accepted decision.
    /// </summary>
    public static RadarProcessingRebalanceRecentAcceptedMove FromDecision(
        RadarProcessingRebalanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (!decision.HasAcceptedMove || decision.Candidate is null)
        {
            throw new ArgumentException("Recent accepted move requires an accepted move decision.", nameof(decision));
        }

        return new RadarProcessingRebalanceRecentAcceptedMove(
            decision.DecisionId,
            decision.EvaluationSequence,
            decision.TopologyVersion,
            decision.ResultTopologyVersion,
            decision.Candidate.MoveKind,
            decision.Candidate.PartitionId,
            decision.Candidate.SourceShardId,
            decision.Candidate.TargetShardId,
            decision.Candidate.ProjectedPressure,
            decision.Candidate.ExpectedRelief);
    }
}
