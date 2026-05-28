using RadarPulse.Application.Product;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;

internal static class RadarPulseProductHandlerFactory
{
    public static IReadOnlyCollection<IRadarSourceProcessingHandler> Create(
        RadarPulseProductHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarPulseProductHandlerSet.None => Array.Empty<IRadarSourceProcessingHandler>(),
            RadarPulseProductHandlerSet.CounterChecksum =>
                RadarProcessingBenchmarkHandlers.Create(RadarProcessingBenchmarkHandlerSet.CounterChecksum),
            RadarPulseProductHandlerSet.CounterChecksumHeavy =>
                RadarProcessingBenchmarkHandlers.Create(RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy),
            RadarPulseProductHandlerSet.SnapshotCounting =>
                new IRadarSourceProcessingHandler[] { new SnapshotCountingHandler() },
            RadarPulseProductHandlerSet.Unsupported =>
                new IRadarSourceProcessingHandler[] { new UnsupportedProductHandler() },
            _ => throw new ArgumentOutOfRangeException(nameof(handlerSet))
        };

    private sealed class SnapshotCountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "product-snapshot-count",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "product.events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }

    private sealed class UnsupportedProductHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "product-unsupported",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "product.unsupported",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
            RadarSourceProcessingHandlerExecutionClassification.Unsupported;

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}
