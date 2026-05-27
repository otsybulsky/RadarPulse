namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceRecentAcceptedMove
{
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

    public long DecisionId { get; }

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingTopologyVersion? ResultTopologyVersion { get; }

    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    public int PartitionId { get; }

    public int SourceShardId { get; }

    public int TargetShardId { get; }

    public RadarProcessingProjectedPressure ProjectedPressure { get; }

    public double ExpectedRelief { get; }

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
