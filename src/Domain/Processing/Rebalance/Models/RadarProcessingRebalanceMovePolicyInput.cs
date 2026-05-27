namespace RadarPulse.Domain.Processing;

/// <summary>
/// Candidate move details evaluated by rebalance policy.
/// </summary>
public readonly record struct RadarProcessingRebalanceMovePolicyInput
{
    /// <summary>
    /// Creates policy input for a candidate owner move.
    /// </summary>
    public RadarProcessingRebalanceMovePolicyInput(
        int partitionId,
        int sourceShardId,
        int targetShardId,
        double projectedBenefit,
        RadarProcessingPressureScore targetProjectedPressure)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceShardId);
        ArgumentOutOfRangeException.ThrowIfNegative(targetShardId);

        if (sourceShardId == targetShardId)
        {
            throw new ArgumentException("Source shard and target shard must be different.", nameof(targetShardId));
        }

        if (double.IsNaN(projectedBenefit) || double.IsInfinity(projectedBenefit) || projectedBenefit < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectedBenefit),
                projectedBenefit,
                "Projected benefit must be finite and non-negative.");
        }

        PartitionId = partitionId;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
        ProjectedBenefit = projectedBenefit;
        TargetProjectedPressure = targetProjectedPressure;
    }

    /// <summary>
    /// Partition proposed for movement.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Current owner shard of the partition.
    /// </summary>
    public int SourceShardId { get; }

    /// <summary>
    /// Proposed target owner shard.
    /// </summary>
    public int TargetShardId { get; }

    /// <summary>
    /// Expected reduction in maximum pressure after the move.
    /// </summary>
    public double ProjectedBenefit { get; }

    /// <summary>
    /// Projected pressure score for the target shard after the move.
    /// </summary>
    public RadarProcessingPressureScore TargetProjectedPressure { get; }
}
