using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmarkTests
{
    private static RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkloadKind workloadKind,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations = 1,
        int warmupIterations = 0) =>
        new RadarProcessingSyntheticRebalanceBenchmark().Measure(
            workloadKind,
            mode,
            iterations,
            warmupIterations);

    private static RadarProcessingSyntheticRebalanceBenchmarkResult Measure(
        RadarProcessingSyntheticRebalanceWorkload workload,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        int orderedActiveBatchCapacity = 1) =>
        new RadarProcessingSyntheticRebalanceBenchmark().Measure(
            workload,
            mode,
            iterations,
            warmupIterations,
            executionMode: executionMode,
            asyncExecution: asyncExecution,
            orderedActiveBatchCapacity: orderedActiveBatchCapacity);
}
