using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticBenchmark
{
    private static RadarProcessingCoreOptions CreateCoreOptions(
        RadarProcessingExecutionMode executionMode,
        int partitionCount,
        int shardCount,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null) =>
        new(
            executionMode,
            partitionCount,
            shardCount,
            enableValidation: true,
            CreateHandlers(handlerSet),
            asyncExecution);

    private static IReadOnlyList<IRadarSourceProcessingHandler> CreateHandlers(
        RadarProcessingBenchmarkHandlerSet handlerSet) =>
        RadarProcessingBenchmarkHandlers.Create(handlerSet);
}
