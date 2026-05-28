using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedBatchResourceTests
{
    [Fact]
    public void PendingCleanupCanReleaseQueueOwnedResources()
    {
        var firstReleased = 0;
        var secondReleased = 0;
        var first = new RadarProcessingRetainedQueuedBatch(
            CreateQueuedBatch(0),
            new RadarProcessingRetainedBatchResource(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: 2,
                release: () =>
                {
                    firstReleased++;
                    return RadarProcessingRetainedPayloadReleaseResult.Released(
                        RadarProcessingRetainedPayloadStrategy.PooledCopy,
                        payloadBytes: 2);
                }));
        var second = new RadarProcessingRetainedQueuedBatch(
            CreateQueuedBatch(1),
            new RadarProcessingRetainedBatchResource(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: 2,
                release: () =>
                {
                    secondReleased++;
                    return RadarProcessingRetainedPayloadReleaseResult.Released(
                        RadarProcessingRetainedPayloadStrategy.PooledCopy,
                        payloadBytes: 2);
                }));

        var cleanup = RadarProcessingRetainedResourceCleanupResult.ReleaseAll(
            [first.Resource, second.Resource]);

        Assert.True(cleanup.IsSuccessful);
        Assert.Equal(2, cleanup.ReleaseAttemptCount);
        Assert.Equal(2, cleanup.ReleasedCount);
        Assert.Equal(0, cleanup.FailedCount);
        Assert.Equal(1, firstReleased);
        Assert.Equal(1, secondReleased);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, first.Resource.State);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, second.Resource.State);
    }

    [Fact]
    public void NotRequiredResourceUsesNotRequiredReleaseStatus()
    {
        var retained = new RadarProcessingRetainedQueuedBatch(CreateQueuedBatch());

        var release = retained.ReleasePending();

        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, retained.Resource.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, release.Status);
        Assert.Equal(RadarProcessingRetainedBatchResourceState.Released, retained.Resource.State);
        Assert.True(retained.HasTerminalResource);
    }
}
