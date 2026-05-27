namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports the accepted state and validation outcome after processing one radar event batch.
/// </summary>
public sealed record RadarProcessingResult
{
    /// <summary>
    /// Creates a processing result and validates telemetry shape against execution mode and topology shape.
    /// </summary>
    public RadarProcessingResult(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingMetrics metrics,
        RadarProcessingValidationResult validation,
        RadarProcessingTelemetry? telemetry = null,
        RadarProcessingTopologyVersion? topologyVersion = null,
        RadarProcessingWorkerTelemetrySummary? workerTelemetry = null)
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
        WorkerTelemetry = workerTelemetry;
    }

    /// <summary>
    /// Gets the execution mode that produced the result.
    /// </summary>
    public RadarProcessingExecutionMode ExecutionMode { get; }

    /// <summary>
    /// Gets the topology version used when the batch was processed.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Gets the partition count for the processing topology.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Gets the shard count for partitioned or async execution.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Gets cumulative processing metrics after the batch outcome.
    /// </summary>
    public RadarProcessingMetrics Metrics { get; }

    /// <summary>
    /// Gets validation status and optional expected metrics evidence.
    /// </summary>
    public RadarProcessingValidationResult Validation { get; }

    /// <summary>
    /// Gets partition and shard telemetry for partitioned or async execution.
    /// </summary>
    public RadarProcessingTelemetry? Telemetry { get; }

    /// <summary>
    /// Gets optional worker telemetry attached by async processing infrastructure.
    /// </summary>
    public RadarProcessingWorkerTelemetrySummary? WorkerTelemetry { get; }

    /// <summary>
    /// Gets whether the processing result passed validation.
    /// </summary>
    public bool IsValid => Validation.IsValid;

    /// <summary>
    /// Returns a copy of the result with worker telemetry attached or replaced.
    /// </summary>
    public RadarProcessingResult WithWorkerTelemetry(
        RadarProcessingWorkerTelemetrySummary? workerTelemetry) =>
        new(
            ExecutionMode,
            PartitionCount,
            ShardCount,
            Metrics,
            Validation,
            Telemetry,
            TopologyVersion,
            workerTelemetry);

    /// <summary>
    /// Creates an empty valid result matching the supplied processing options.
    /// </summary>
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

        if (executionMode is not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentException(
                "Telemetry is only supported for partitioned barrier or async shard transport processing.",
                nameof(telemetry));
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
