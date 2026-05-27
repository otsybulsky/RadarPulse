namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports how many benchmark events were routed to one shard.
/// </summary>
/// <param name="ShardId">Shard id represented by the distribution entry.</param>
/// <param name="EventCount">Number of benchmark events routed to the shard.</param>
public readonly record struct RadarProcessingBenchmarkShardDistribution(
    int ShardId,
    long EventCount);
