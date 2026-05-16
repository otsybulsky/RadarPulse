namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingResult
{
    public RadarProcessingResult(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingMetrics metrics,
        RadarProcessingValidationResult validation)
    {
        RadarProcessingCoreOptions.EnsureKnownExecutionMode(executionMode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        ArgumentNullException.ThrowIfNull(validation);

        ExecutionMode = executionMode;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        Metrics = metrics;
        Validation = validation;
    }

    public RadarProcessingExecutionMode ExecutionMode { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public RadarProcessingMetrics Metrics { get; }

    public RadarProcessingValidationResult Validation { get; }

    public bool IsValid => Validation.IsValid;

    public static RadarProcessingResult Empty(RadarProcessingCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RadarProcessingResult(
            options.ExecutionMode,
            options.PartitionCount,
            options.ShardCount,
            RadarProcessingMetrics.Empty,
            RadarProcessingValidationResult.Valid(RadarProcessingMetrics.Empty));
    }
}
