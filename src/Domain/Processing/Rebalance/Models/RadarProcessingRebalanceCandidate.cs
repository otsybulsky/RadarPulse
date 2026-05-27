namespace RadarPulse.Domain.Processing;

/// <summary>
/// Planner-selected partition move candidate before policy or publication.
/// </summary>
/// <remarks>
/// Candidates carry both projected source/target pressure and expected relief so
/// policy can apply benefit and target-headroom checks without re-running the
/// planner.
/// </remarks>
public sealed class RadarProcessingRebalanceCandidate
{
    /// <summary>
    /// Creates a rebalance candidate for moving one partition between shards.
    /// </summary>
    public RadarProcessingRebalanceCandidate(
        RadarProcessingRebalanceMoveKind moveKind,
        int partitionId,
        int sourceShardId,
        int targetShardId,
        RadarProcessingProjectedPressure projectedPressure,
        double expectedRelief)
    {
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

        if (double.IsNaN(expectedRelief) || double.IsInfinity(expectedRelief) || expectedRelief < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedRelief),
                expectedRelief,
                "Expected relief must be finite and non-negative.");
        }

        MoveKind = moveKind;
        PartitionId = partitionId;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
        ProjectedPressure = projectedPressure;
        ExpectedRelief = expectedRelief;
    }

    /// <summary>
    /// Strategy that produced the candidate.
    /// </summary>
    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    /// <summary>
    /// Partition proposed for movement.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Current owner shard.
    /// </summary>
    public int SourceShardId { get; }

    /// <summary>
    /// Proposed target owner shard.
    /// </summary>
    public int TargetShardId { get; }

    /// <summary>
    /// Projected pressure before and after the move.
    /// </summary>
    public RadarProcessingProjectedPressure ProjectedPressure { get; }

    /// <summary>
    /// Expected reduction in maximum shard pressure.
    /// </summary>
    public double ExpectedRelief { get; }

    /// <summary>
    /// Converts the candidate to policy input.
    /// </summary>
    public RadarProcessingRebalanceMovePolicyInput ToPolicyInput() =>
        new(
            PartitionId,
            SourceShardId,
            TargetShardId,
            ExpectedRelief,
            ProjectedPressure.TargetShardAfter);

    /// <summary>
    /// Converts the candidate to a topology move request for a specific expected version.
    /// </summary>
    public RadarProcessingTopologyMoveRequest ToTopologyMoveRequest(
        RadarProcessingTopologyVersion expectedTopologyVersion) =>
        new(
            expectedTopologyVersion,
            PartitionId,
            SourceShardId,
            TargetShardId);
}
