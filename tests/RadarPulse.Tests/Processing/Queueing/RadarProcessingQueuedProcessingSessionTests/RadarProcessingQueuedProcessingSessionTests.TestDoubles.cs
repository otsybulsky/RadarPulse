using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProcessingSessionTests
{
    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action dispose;

        public CallbackDisposable(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose()
        {
            dispose();
        }
    }

    private sealed class ThrowingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new("throwing", int64SlotCount: 0, doubleSlotCount: 0);

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state) =>
            throw new InvalidOperationException("handler failed");
    }
}
