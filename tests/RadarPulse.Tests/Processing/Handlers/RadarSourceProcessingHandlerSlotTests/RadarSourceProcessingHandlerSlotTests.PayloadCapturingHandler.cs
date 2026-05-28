using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarSourceProcessingHandlerSlotTests
{
    private sealed class PayloadCapturingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "payload",
                int64SlotCount: 0,
                doubleSlotCount: 0);

        public int InvocationCount { get; private set; }

        public int LastPayloadLength { get; private set; }

        public byte LastFirstPayloadByte { get; private set; }

        public long LastPayloadValueCount { get; private set; }

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            InvocationCount++;
            LastPayloadLength = context.Payload.Length;
            LastFirstPayloadByte = context.Payload[0];
            LastPayloadValueCount = context.PayloadMetrics.PayloadValueCount;
        }
    }
}
