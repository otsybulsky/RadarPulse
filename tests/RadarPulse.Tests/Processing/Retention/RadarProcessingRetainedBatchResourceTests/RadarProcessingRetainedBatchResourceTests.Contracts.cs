using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedBatchResourceTests
{
    [Fact]
    public void ResourceRejectsInvalidConstructionAndTransitions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedBatchResource(
                (RadarProcessingRetainedPayloadStrategy)255,
                payloadBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedBatchResource(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: -1));

        var resource = new RadarProcessingRetainedBatchResource(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            payloadBytes: 2);

        Assert.Throws<InvalidOperationException>(resource.TransferToConsumer);
        resource.TransferToQueue();
        resource.TransferToConsumer();
        Assert.Throws<InvalidOperationException>(resource.TransferToConsumer);
        resource.Release();
        Assert.Throws<InvalidOperationException>(resource.TransferToQueue);
    }

    [Fact]
    public void CleanupResultCopiesReleaseResultsAndRejectsNulls()
    {
        var releases = new List<RadarProcessingRetainedPayloadReleaseResult>
        {
            RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            RadarProcessingRetainedPayloadReleaseResult.AlreadyReleased(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            RadarProcessingRetainedPayloadReleaseResult.NotRequired(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy)
        };

        var cleanup = new RadarProcessingRetainedResourceCleanupResult(releases);
        releases.Clear();

        Assert.True(cleanup.IsSuccessful);
        Assert.Equal(3, cleanup.ReleaseAttemptCount);
        Assert.Equal(1, cleanup.ReleasedCount);
        Assert.Equal(1, cleanup.AlreadyReleasedCount);
        Assert.Equal(1, cleanup.NotRequiredCount);
        Assert.Equal(0, cleanup.FailedCount);
        Assert.Equal(3, cleanup.ReleaseResults.Count);
        Assert.Empty(RadarProcessingRetainedResourceCleanupResult.Empty.ReleaseResults);

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingRetainedResourceCleanupResult(
                new RadarProcessingRetainedPayloadReleaseResult[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedResourceCleanupResult.ReleaseAll(null!));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedResourceCleanupResult.ReleaseAll(
                new RadarProcessingRetainedBatchResource[] { null! }));
    }
}
