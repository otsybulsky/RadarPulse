using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

/// <summary>
/// CLI options for synthetic processing rebalance benchmark runs.
/// </summary>
public sealed partial record ProcessingBenchmarkRebalanceSyntheticOptions(
    IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> Workloads,
    IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> Modes,
    RadarProcessingValidationProfile ValidationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides QuarantineLifecycleOverrides,
    int Iterations,
    int WarmupIterations,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingAsyncExecutionOptions? AsyncExecution = null,
    int OrderedActiveBatchCapacityValue = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity)
{
    private const int DefaultAsyncWorkerCount = 2;
    private const int DefaultAsyncQueueCapacity = 1;

    /// <summary>
    /// Gets the active batch capacity used by ordered rebalance sessions.
    /// </summary>
    public int OrderedActiveBatchCapacity => OrderedActiveBatchCapacityValue;
}
