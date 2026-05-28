using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingMvpHandlerDeltaRuntimeTests
{
    private sealed class UnsupportedCountingHandler :
        IRadarSourceProcessingHandler,
        IRadarSourceProcessingHandlerExecutionMetadata
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "unsupported",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
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
