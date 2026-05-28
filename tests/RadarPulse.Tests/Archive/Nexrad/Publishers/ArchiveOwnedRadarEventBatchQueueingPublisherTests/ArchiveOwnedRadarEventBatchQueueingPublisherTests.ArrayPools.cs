using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
    private sealed class ThrowingReturnArrayPool<T> : ArrayPool<T>
    {
        public override T[] Rent(int minimumLength) => new T[minimumLength];

        public override void Return(T[] array, bool clearArray = false) =>
            throw new InvalidOperationException("pool return failed");
    }

    private sealed class FailingRentArrayPool<T> : ArrayPool<T>
    {
        private readonly int successfulRentCount;
        private int rentCount;

        public FailingRentArrayPool(int successfulRentCount)
        {
            this.successfulRentCount = successfulRentCount;
        }

        public override T[] Rent(int minimumLength)
        {
            if (rentCount++ >= successfulRentCount)
            {
                throw new InvalidOperationException("pool rent failed");
            }

            return new T[minimumLength];
        }

        public override void Return(T[] array, bool clearArray = false)
        {
        }
    }
}
