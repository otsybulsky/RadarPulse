namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceCandidate
{
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

    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    public int PartitionId { get; }

    public int SourceShardId { get; }

    public int TargetShardId { get; }

    public RadarProcessingProjectedPressure ProjectedPressure { get; }

    public double ExpectedRelief { get; }

    public RadarProcessingRebalanceMovePolicyInput ToPolicyInput() =>
        new(
            PartitionId,
            SourceShardId,
            TargetShardId,
            ExpectedRelief,
            ProjectedPressure.TargetShardAfter);

    public RadarProcessingTopologyMoveRequest ToTopologyMoveRequest(
        RadarProcessingTopologyVersion expectedTopologyVersion) =>
        new(
            expectedTopologyVersion,
            PartitionId,
            SourceShardId,
            TargetShardId);
}
