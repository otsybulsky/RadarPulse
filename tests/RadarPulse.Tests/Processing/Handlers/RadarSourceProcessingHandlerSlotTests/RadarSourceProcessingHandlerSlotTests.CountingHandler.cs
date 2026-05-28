using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarSourceProcessingHandlerSlotTests
{
    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public CountingHandler(
            string name = "counting",
            string eventFieldName = "events",
            string payloadFieldName = "payload.values",
            string checksumFieldName = "raw.checksum",
            string scaleFieldName = "last.scale")
        {
            Descriptor = new RadarSourceProcessingHandlerDescriptor(
                name,
                int64SlotCount: 3,
                doubleSlotCount: 1,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        eventFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        payloadFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 1),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        checksumFieldName,
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 2),
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        scaleFieldName,
                        RadarSourceProcessingSnapshotFieldType.Double,
                        slotIndex: 0)
                });
        }

        public RadarSourceProcessingHandlerDescriptor Descriptor { get; }

        public int InvocationCount { get; private set; }

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            InvocationCount++;
            state.AddInt64(slotIndex: 0, value: 1);
            state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
            state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
            state.SetDouble(slotIndex: 0, context.StreamEvent.Scale);
        }
    }
}
