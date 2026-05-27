namespace RadarPulse.Domain.Processing;

/// <summary>
/// Remaining cooldown for moving work from or to a shard.
/// </summary>
public readonly record struct RadarProcessingShardCooldown
{
    /// <summary>
    /// Creates a shard cooldown snapshot.
    /// </summary>
    public RadarProcessingShardCooldown(
        int shardId,
        int remainingEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingEvaluations);

        ShardId = shardId;
        RemainingEvaluations = remainingEvaluations;
    }

    /// <summary>
    /// Shard whose cooldown is being tracked.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Remaining rebalance evaluations before the shard can participate again.
    /// </summary>
    public int RemainingEvaluations { get; }

    /// <summary>
    /// Indicates whether the cooldown currently blocks participation.
    /// </summary>
    public bool IsActive => RemainingEvaluations > 0;
}
