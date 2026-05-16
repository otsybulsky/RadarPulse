namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingResult
{
    public RadarProcessingResult(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingMetrics metrics,
        RadarProcessingValidationResult validation,
        RadarProcessingTelemetry? telemetry = null,
        RadarProcessingTopologyVersion? topologyVersion = null)
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
        var resolvedTopologyVersion =
            topologyVersion ??
            telemetry?.TopologyVersion ??
            RadarProcessingTopologyVersion.Initial;
        ValidateTelemetry(executionMode, partitionCount, shardCount, resolvedTopologyVersion, telemetry);

        ExecutionMode = executionMode;
        TopologyVersion = resolvedTopologyVersion;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        Metrics = metrics;
        Validation = validation;
        Telemetry = telemetry;
    }

    public RadarProcessingExecutionMode ExecutionMode { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public RadarProcessingMetrics Metrics { get; }

    public RadarProcessingValidationResult Validation { get; }

    public RadarProcessingTelemetry? Telemetry { get; }

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

    private static void ValidateTelemetry(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingTelemetry? telemetry)
    {
        if (telemetry is null)
        {
            return;
        }

        if (executionMode != RadarProcessingExecutionMode.PartitionedBarrier)
        {
            throw new ArgumentException("Telemetry is only supported for partitioned barrier processing.", nameof(telemetry));
        }

        if (telemetry.ExecutionMode != executionMode)
        {
            throw new ArgumentException("Telemetry execution mode must match result execution mode.", nameof(telemetry));
        }

        if (telemetry.PartitionCount != partitionCount)
        {
            throw new ArgumentException("Telemetry partition count must match result partition count.", nameof(telemetry));
        }

        if (telemetry.ShardCount != shardCount)
        {
            throw new ArgumentException("Telemetry shard count must match result shard count.", nameof(telemetry));
        }

        if (telemetry.TopologyVersion != topologyVersion)
        {
            throw new ArgumentException("Telemetry topology version must match result topology version.", nameof(telemetry));
        }
    }
}
