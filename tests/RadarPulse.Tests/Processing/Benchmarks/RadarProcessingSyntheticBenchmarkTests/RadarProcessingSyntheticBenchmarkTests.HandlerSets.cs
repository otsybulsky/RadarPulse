using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticBenchmarkTests
{
    [Fact]
    public void BenchmarkHandlerFactoryCreatesMergeableCounterChecksumHeavySet()
    {
        var handlers = RadarProcessingBenchmarkHandlers.Create(
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy);

        Assert.Equal(2, handlers.Count);
        Assert.All(
            handlers,
            handler =>
            {
                var metadata = Assert.IsAssignableFrom<IRadarSourceProcessingHandlerExecutionMetadata>(handler);
                Assert.Equal(
                    RadarSourceProcessingHandlerExecutionClassification.Mergeable,
                    metadata.ExecutionClassification);
                var merger = Assert.IsAssignableFrom<IRadarProcessingHandlerDeltaMerger>(handler);
                Assert.Equal(handler.Descriptor.Name, merger.HandlerName);
                var factory = Assert.IsAssignableFrom<IRadarProcessingHandlerDeltaAccumulatorFactory>(handler);
                Assert.NotNull(factory.CreateAccumulator());
            });
        var fieldNames = handlers
            .SelectMany(static handler => handler.Descriptor.SnapshotFields)
            .Select(static field => field.Name)
            .ToArray();
        Assert.Equal(fieldNames.Length, fieldNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SyntheticBenchmarkAcceptsCounterChecksumHeavyHandlerSet()
    {
        var workloadOptions = new RadarProcessingSyntheticWorkloadOptions(
            SourceCount: 4,
            BatchCount: 1,
            EventsPerBatch: 8,
            PayloadValuesPerEvent: 4);

        var result = new RadarProcessingSyntheticBenchmark().Measure(
            workloadOptions,
            RadarProcessingExecutionMode.Sequential,
            partitionCount: 1,
            shardCount: 1,
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy,
            iterations: 1,
            warmupIterations: 0,
            CancellationToken.None);

        Assert.Equal(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy, result.HandlerSet);
        Assert.Equal(8, result.EventsPerIteration);
        Assert.Equal(32, result.PayloadValuesPerIteration);
        Assert.NotEqual(0UL, result.ValidationChecksum);
    }
}
