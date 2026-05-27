namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionCooldown
{
    public RadarProcessingPartitionCooldown(
        int partitionId,
        int remainingEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingEvaluations);

        PartitionId = partitionId;
        RemainingEvaluations = remainingEvaluations;
    }

    public int PartitionId { get; }

    public int RemainingEvaluations { get; }

    public bool IsActive => RemainingEvaluations > 0;
}
