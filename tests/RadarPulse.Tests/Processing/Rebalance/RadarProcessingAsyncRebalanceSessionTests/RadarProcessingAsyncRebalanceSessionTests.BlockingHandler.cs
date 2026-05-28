using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncRebalanceSessionTests
{
    private sealed class BlockingHandler : IRadarSourceProcessingHandler
    {
        private readonly ManualResetEventSlim entered = new();
        private readonly ManualResetEventSlim released = new();
        private int blocked;

        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "blocking",
                int64SlotCount: 0,
                doubleSlotCount: 0);

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            if (Interlocked.Exchange(ref blocked, 1) != 0)
            {
                return;
            }

            entered.Set();
            released.Wait();
        }

        public bool WaitUntilEntered(TimeSpan timeout) =>
            entered.Wait(timeout);

        public void Release() =>
            released.Set();
    }
}
