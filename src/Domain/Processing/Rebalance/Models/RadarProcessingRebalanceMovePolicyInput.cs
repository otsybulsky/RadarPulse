namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingRebalanceMovePolicyInput
{
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

    public int PartitionId { get; }

    public int SourceShardId { get; }

    public int TargetShardId { get; }

    public double ProjectedBenefit { get; }

    public RadarProcessingPressureScore TargetProjectedPressure { get; }
}
