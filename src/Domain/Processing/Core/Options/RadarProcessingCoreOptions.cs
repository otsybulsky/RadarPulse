namespace RadarPulse.Domain.Processing;

/// <summary>
/// Configures the core processing engine, topology shape, validation, handlers, and async worker settings.
/// </summary>
public sealed record RadarProcessingCoreOptions
{
    /// <summary>
    /// Gets the default single-partition sequential processing configuration.
    /// </summary>
    public static RadarProcessingCoreOptions Default { get; } = new();

    /// <summary>
    /// Creates processing core options and validates topology, execution mode, and handler layout constraints.
    /// </summary>
    public RadarProcessingCoreOptions(
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.Sequential,
        int partitionCount = 1,
        int shardCount = 1,
        bool enableValidation = true,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null)
    {
        EnsureKnownExecutionMode(executionMode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        ExecutionMode = executionMode;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        EnableValidation = enableValidation;
        AsyncExecution = asyncExecution ?? RadarProcessingAsyncExecutionOptions.Default;
        HandlerSlotLayout = new RadarSourceProcessingHandlerSlotLayout(handlers);
    }

    /// <summary>
    /// Gets the engine mode used when processing batches.
    /// </summary>
    public RadarProcessingExecutionMode ExecutionMode { get; }

    /// <summary>
    /// Gets the number of topology partitions assigned across shards.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Gets the number of shard work units used by partitioned and async execution.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Gets whether the core validates batch schema, source ownership, and output metrics.
    /// </summary>
    public bool EnableValidation { get; }

    /// <summary>
    /// Gets async worker settings used when <see cref="ExecutionMode"/> is async shard transport.
    /// </summary>
    public RadarProcessingAsyncExecutionOptions AsyncExecution { get; }

    /// <summary>
    /// Gets the custom source handlers applied as part of processing state updates.
    /// </summary>
    public IReadOnlyList<IRadarSourceProcessingHandler> Handlers => HandlerSlotLayout.Handlers;

    internal RadarSourceProcessingHandlerSlotLayout HandlerSlotLayout { get; }

    internal static void EnsureKnownExecutionMode(RadarProcessingExecutionMode executionMode)
    {
        if (executionMode is not RadarProcessingExecutionMode.Sequential and
            not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
        }
    }
}
