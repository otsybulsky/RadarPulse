namespace RadarPulse.Domain.Processing;

/// <summary>
/// Remaining cooldown for moving a specific partition again.
/// </summary>
public readonly record struct RadarProcessingPartitionCooldown
{
    /// <summary>
    /// Creates a partition cooldown snapshot.
    /// </summary>
    public RadarProcessingPartitionCooldown(
        int partitionId,
        int remainingEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingEvaluations);

        PartitionId = partitionId;
        RemainingEvaluations = remainingEvaluations;
    }

    /// <summary>
    /// Partition whose cooldown is being tracked.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Remaining rebalance evaluations before another move is allowed.
    /// </summary>
    public int RemainingEvaluations { get; }

    /// <summary>
    /// Indicates whether the cooldown currently blocks moves.
    /// </summary>
    public bool IsActive => RemainingEvaluations > 0;
}
