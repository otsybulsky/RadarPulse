using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Factory for accepted archive-shaped processing runtime defaults.
/// </summary>
public static class RadarProcessingRuntimeArchiveBaseline
{
    /// <summary>
    /// Baseline processing execution mode.
    /// </summary>
    public const RadarProcessingExecutionMode ExecutionMode =
        RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;
    /// <summary>
    /// Baseline async worker count.
    /// </summary>
    public const int WorkerCount = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount;
    /// <summary>
    /// Baseline per-worker queue capacity.
    /// </summary>
    public const int WorkerQueueCapacity = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity;
    /// <summary>
    /// Baseline ordered active batch capacity.
    /// </summary>
    public const int OrderedActiveBatchCapacity =
        RadarProcessingOrderedConcurrencyOptions.DefaultActiveBatchCapacity;

    /// <summary>
    /// Baseline queued-overlap options.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapOptions QueuedOverlapOptions =>
        RadarProcessingArchiveQueuedOverlapOptions.Default;

    /// <summary>
    /// Baseline ordered concurrency options.
    /// </summary>
    public static RadarProcessingOrderedConcurrencyOptions OrderedConcurrencyOptions =>
        RadarProcessingOrderedConcurrencyOptions.Default;

    /// <summary>
    /// Creates baseline async execution options.
    /// </summary>
    public static RadarProcessingAsyncExecutionOptions CreateAsyncExecution() =>
        RadarProcessingArchiveRebalanceRolloutDefaults.CreateAsyncExecution();

    /// <summary>
    /// Creates processing core options for the baseline archive runtime.
    /// </summary>
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

    /// <summary>
    /// Creates a processing core using baseline archive runtime options.
    /// </summary>
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

    /// <summary>
    /// Creates a rebalance session using baseline archive runtime core options.
    /// </summary>
    public static RadarProcessingRebalanceSession CreateRebalanceSession(
        RadarSourceUniverse sourceUniverse,
        int partitionCount,
        int shardCount,
        bool enableValidation = true,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null,
        RadarProcessingPressureOptions? pressureOptions = null,
        RadarProcessingPressureWindow? pressureWindow = null,
        RadarProcessingRebalancePolicyState? policyState = null,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier = null,
        RadarProcessingDirectHotReliefPlanner? directHotReliefPlanner = null,
        RadarProcessingColdEvacuationPlanner? coldEvacuationPlanner = null,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null,
        RadarProcessingRebalanceTelemetryRecorder? telemetryRecorder = null,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingPressureSkewOptions? pressureSkewOptions = null) =>
        new(
            CreateCore(
                sourceUniverse,
                partitionCount,
                shardCount,
                enableValidation,
                handlers),
            pressureOptions,
            pressureWindow,
            policyState,
            hotPartitionClassifier,
            directHotReliefPlanner,
            coldEvacuationPlanner,
            quarantineLifecycleTracker,
            telemetryRecorder,
            hardeningOptions,
            pressureSkewOptions);

    /// <summary>
    /// Checks whether core options match the baseline runtime contour.
    /// </summary>
    public static bool MatchesCoreOptions(RadarProcessingCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ExecutionMode == ExecutionMode &&
               options.AsyncExecution.WorkerCount == WorkerCount &&
               options.AsyncExecution.QueueCapacity == WorkerQueueCapacity;
    }

    /// <summary>
    /// Checks whether queued-overlap options match the baseline runtime contour.
    /// </summary>
    public static bool MatchesQueuedOverlapOptions(
        RadarProcessingArchiveQueuedOverlapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.IsRuntimeDefaultContour;
    }

    /// <summary>
    /// Checks whether ordered concurrency options match the baseline runtime contour.
    /// </summary>
    public static bool MatchesOrderedConcurrencyOptions(
        RadarProcessingOrderedConcurrencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ActiveBatchCapacity == OrderedActiveBatchCapacity;
    }
}
