using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadContractTests
{
    [Fact]
    public void ReleaseResultsSeparateLifecycleStatuses()
    {
        var released = RadarProcessingRetainedPayloadReleaseResult.Released(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            TimeSpan.FromMilliseconds(2),
            payloadBytes: 64,
            poolReturnCount: 2,
            eventPoolReturnCount: 1,
            payloadPoolReturnCount: 1);
        var alreadyReleased = RadarProcessingRetainedPayloadReleaseResult.AlreadyReleased(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            "already returned");
        var failed = RadarProcessingRetainedPayloadReleaseResult.Failed(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            "return failed");
        var notRequired = RadarProcessingRetainedPayloadReleaseResult.NotRequired(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy);

        Assert.True(released.IsReleased);
        Assert.False(released.IsFailure);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, released.Status);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, released.Strategy);
        Assert.Equal(TimeSpan.FromMilliseconds(2), released.Elapsed);
        Assert.Equal(64, released.PayloadBytes);
        Assert.Equal(2, released.PoolReturnCount);
        Assert.Equal(1, released.EventPoolReturnCount);
        Assert.Equal(1, released.PayloadPoolReturnCount);

        Assert.False(alreadyReleased.IsReleased);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased, alreadyReleased.Status);
        Assert.Equal("already returned", alreadyReleased.Message);
        Assert.True(failed.IsFailure);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Failed, failed.Status);
        Assert.Equal("return failed", failed.Message);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, notRequired.Status);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Released(
                (RadarProcessingRetainedPayloadStrategy)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                payloadBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                poolReturnCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Released(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                eventPoolReturnCount: -1));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Failed(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                null!));
    }
}
