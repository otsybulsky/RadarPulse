using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarProcessingBenchmarkHandlers
{
    private sealed class CounterChecksumBenchmarkHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata,
        IRadarProcessingHandlerDeltaMerger,
        IRadarProcessingHandlerDeltaAccumulatorFactory
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "benchmark.counter_checksum",
                int64SlotCount: 3,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.payload_values",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "benchmark.raw_checksum",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Mergeable;

        public string HandlerName => "benchmark.counter_checksum";

        public string HandlerContractVersion => "v1";

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
        }

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta) =>
            MergeInt64Values(currentValues, delta);

        public IRadarProcessingHandlerDeltaAccumulator CreateAccumulator() =>
            new Int64SumHandlerDeltaAccumulator();
    }
}
