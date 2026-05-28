using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactoryTests
{
    private sealed class TrackingArrayPool<T> : ArrayPool<T>
    {
        public int RentCount { get; private set; }

        public int ReturnCount { get; private set; }

        public override T[] Rent(int minimumLength)
        {
            RentCount++;
            return new T[minimumLength];
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            ArgumentNullException.ThrowIfNull(array);
            ReturnCount++;
            if (clearArray)
            {
                Array.Clear(array);
            }
        }
    }

    private sealed class ThrowingArrayPool<T> : ArrayPool<T>
    {
        public override T[] Rent(int minimumLength) =>
            throw new InvalidOperationException("rent failed");

        public override void Return(T[] array, bool clearArray = false)
        {
        }
    }
}
