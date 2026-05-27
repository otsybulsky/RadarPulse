namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingShardCooldown
{
    public RadarProcessingShardCooldown(
        int shardId,
        int remainingEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingEvaluations);

        ShardId = shardId;
        RemainingEvaluations = remainingEvaluations;
    }

    public int ShardId { get; }

    public int RemainingEvaluations { get; }

    public bool IsActive => RemainingEvaluations > 0;
}
