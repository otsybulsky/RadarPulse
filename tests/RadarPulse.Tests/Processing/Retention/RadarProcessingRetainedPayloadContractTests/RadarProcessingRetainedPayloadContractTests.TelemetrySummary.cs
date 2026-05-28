using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadContractTests
{
    [Fact]
    public void RetainedPayloadTelemetrySummaryCarriesCountersAndRejectsInvalidShapes()
    {
        var summary = new RadarProcessingRetainedPayloadTelemetrySummary(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            retentionAttemptCount: 4,
            retainedBatchCount: 2,
            retentionUnsupportedStrategyCount: 1,
            retentionFailedCopyCount: 1,
            retainedEventCount: 5,
            retainedPayloadBytes: 128,
            retainedPayloadValueCount: 64,
            allocatedBytes: 256,
            totalRetentionTime: TimeSpan.FromMilliseconds(6),
            transferCount: 0,
            poolRentCount: 2,
            poolReturnCount: 1,
            poolMissCount: 1,
            releaseAttemptCount: 3,
            releasedBatchCount: 1,
            alreadyReleasedBatchCount: 1,
            releaseFailedCount: 1,
            totalReleaseTime: TimeSpan.FromMilliseconds(2),
            eventPoolRentCount: 1,
            eventPoolReturnCount: 1,
            eventPoolMissCount: 1,
            payloadPoolRentCount: 1,
            payloadPoolReturnCount: 0,
            payloadPoolMissCount: 0);

        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, summary.Strategy);
        Assert.Equal(4, summary.RetentionAttemptCount);
        Assert.Equal(2, summary.RetainedBatchCount);
        Assert.Equal(1, summary.RetentionUnsupportedStrategyCount);
        Assert.Equal(1, summary.RetentionFailedCopyCount);
        Assert.Equal(5, summary.RetainedEventCount);
        Assert.Equal(128, summary.RetainedPayloadBytes);
        Assert.Equal(64, summary.RetainedPayloadValueCount);
        Assert.Equal(256, summary.AllocatedBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(6), summary.TotalRetentionTime);
        Assert.Equal(2, summary.PoolRentCount);
        Assert.Equal(1, summary.PoolReturnCount);
        Assert.Equal(1, summary.PoolMissCount);
        Assert.Equal(1, summary.EventPoolRentCount);
        Assert.Equal(1, summary.EventPoolReturnCount);
        Assert.Equal(1, summary.EventPoolMissCount);
        Assert.Equal(1, summary.PayloadPoolRentCount);
        Assert.Equal(0, summary.PayloadPoolReturnCount);
        Assert.Equal(0, summary.PayloadPoolMissCount);
        Assert.Equal(3, summary.ReleaseAttemptCount);
        Assert.Equal(1, summary.ReleasedBatchCount);
        Assert.Equal(1, summary.AlreadyReleasedBatchCount);
        Assert.Equal(1, summary.ReleaseFailedCount);
        Assert.Equal(TimeSpan.FromMilliseconds(2), summary.TotalReleaseTime);
        Assert.Equal(2, summary.FailedRetentionCount);
        Assert.True(summary.HasFailures);
        Assert.Equal(128.0, summary.AllocatedBytesPerRetainedBatch);
        Assert.Equal(4.0, summary.AllocatedBytesPerPayloadValue);

        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, RadarProcessingRetainedPayloadTelemetrySummary.Empty.Strategy);
        Assert.False(RadarProcessingRetainedPayloadTelemetrySummary.Empty.HasFailures);
        Assert.Equal(0.0, RadarProcessingRetainedPayloadTelemetrySummary.Empty.AllocatedBytesPerPayloadValue);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadTelemetrySummary((RadarProcessingRetainedPayloadStrategy)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadTelemetrySummary(retentionAttemptCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadTelemetrySummary(totalRetentionTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadTelemetrySummary(totalReleaseTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadTelemetrySummary(
                retentionAttemptCount: 1,
                retainedBatchCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadTelemetrySummary(
                releaseAttemptCount: 1,
                releasedBatchCount: 1,
                releaseFailedCount: 1));
    }
}
