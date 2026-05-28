using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactory
{
    private sealed class PooledRetainedPayloadReleaseOwner
    {
        private readonly ArrayPool<RadarStreamEvent> eventPool;
        private readonly ArrayPool<byte> payloadPool;
        private readonly RadarStreamEvent[]? eventArray;
        private readonly byte[]? payloadArray;
        private readonly int payloadBytes;

        public PooledRetainedPayloadReleaseOwner(
            ArrayPool<RadarStreamEvent> eventPool,
            ArrayPool<byte> payloadPool,
            RadarStreamEvent[]? eventArray,
            byte[]? payloadArray,
            int payloadBytes)
        {
            this.eventPool = eventPool ?? throw new ArgumentNullException(nameof(eventPool));
            this.payloadPool = payloadPool ?? throw new ArgumentNullException(nameof(payloadPool));
            ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);

            this.eventArray = eventArray;
            this.payloadArray = payloadArray;
            this.payloadBytes = payloadBytes;
        }

        public RadarProcessingRetainedPayloadReleaseResult Release()
        {
            long poolReturnCount = 0;
            long eventPoolReturnCount = 0;
            long payloadPoolReturnCount = 0;
            if (eventArray is not null)
            {
                eventPool.Return(eventArray);
                poolReturnCount++;
                eventPoolReturnCount++;
            }

            if (payloadArray is not null)
            {
                payloadPool.Return(payloadArray);
                poolReturnCount++;
                payloadPoolReturnCount++;
            }

            return RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: payloadBytes,
                poolReturnCount: poolReturnCount,
                eventPoolReturnCount: eventPoolReturnCount,
                payloadPoolReturnCount: payloadPoolReturnCount);
        }
    }
}
