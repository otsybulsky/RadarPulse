namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingCoreOptions
{
    public static RadarProcessingCoreOptions Default { get; } = new();

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

    public RadarProcessingExecutionMode ExecutionMode { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public bool EnableValidation { get; }

    public RadarProcessingAsyncExecutionOptions AsyncExecution { get; }

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
