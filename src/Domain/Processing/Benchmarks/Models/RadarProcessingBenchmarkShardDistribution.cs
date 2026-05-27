namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingBenchmarkShardDistribution(
    int ShardId,
    long EventCount);
