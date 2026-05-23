using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public static class RadarProcessingRuntimeArchiveBaseline
{
    public const RadarProcessingExecutionMode ExecutionMode =
        RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;
    public const int WorkerCount = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount;
    public const int WorkerQueueCapacity = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity;

    public static RadarProcessingArchiveQueuedOverlapOptions QueuedOverlapOptions =>
        RadarProcessingArchiveQueuedOverlapOptions.Default;

    public static RadarProcessingAsyncExecutionOptions CreateAsyncExecution() =>
        RadarProcessingArchiveRebalanceRolloutDefaults.CreateAsyncExecution();

    public static RadarProcessingCoreOptions CreateCoreOptions(
        int partitionCount,
        int shardCount,
        bool enableValidation = true,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null) =>
        new(
            ExecutionMode,
            partitionCount,
            shardCount,
            enableValidation,
            handlers,
            CreateAsyncExecution());

    public static RadarProcessingCore CreateCore(
        RadarSourceUniverse sourceUniverse,
        int partitionCount,
        int shardCount,
        bool enableValidation = true,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        return new RadarProcessingCore(
            sourceUniverse,
            CreateCoreOptions(
                partitionCount,
                shardCount,
                enableValidation,
                handlers));
    }

    public static bool MatchesCoreOptions(RadarProcessingCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ExecutionMode == ExecutionMode &&
               options.AsyncExecution.WorkerCount == WorkerCount &&
               options.AsyncExecution.QueueCapacity == WorkerQueueCapacity;
    }

    public static bool MatchesQueuedOverlapOptions(
        RadarProcessingArchiveQueuedOverlapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.IsRuntimeDefaultContour;
    }
}
