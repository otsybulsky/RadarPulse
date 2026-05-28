using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingOwnedBatchQueueTests
{
    [Fact]
    public void OwnedBatchDequeueContractsRejectInvalidShapes()
    {
        Assert.Equal(1, (int)RadarProcessingOwnedBatchDequeueStatus.Item);
        Assert.Equal(2, (int)RadarProcessingOwnedBatchDequeueStatus.Closed);
        Assert.Equal(3, (int)RadarProcessingOwnedBatchDequeueStatus.Canceled);
        Assert.Equal(4, (int)RadarProcessingOwnedBatchDequeueStatus.Faulted);
        Assert.Equal(5, (int)RadarProcessingOwnedBatchDequeueStatus.Disposed);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingOwnedBatchDequeueResult((RadarProcessingOwnedBatchDequeueStatus)255));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingOwnedBatchDequeueResult(RadarProcessingOwnedBatchDequeueStatus.Item));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Closed,
                new RadarProcessingQueuedBatch(RadarProcessingQueuedBatchSequence.Initial, CreateOwnedBatch(1))));
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Closed,
                message: null!));
    }
}
