using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRetainedPayloadContractTests
{
    [Fact]
    public void RetainedPayloadEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingRetainedPayloadStrategy.SnapshotCopy);
        Assert.Equal(2, (int)RadarProcessingRetainedPayloadStrategy.PooledCopy);
        Assert.Equal(3, (int)RadarProcessingRetainedPayloadStrategy.BuilderTransfer);

        Assert.Equal(1, (int)RadarProcessingRetainedPayloadRetentionStatus.Succeeded);
        Assert.Equal(2, (int)RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy);
        Assert.Equal(3, (int)RadarProcessingRetainedPayloadRetentionStatus.FailedCopy);
        Assert.Equal(4, (int)RadarProcessingRetainedPayloadRetentionStatus.Canceled);
        Assert.Equal(5, (int)RadarProcessingRetainedPayloadRetentionStatus.InvalidInput);

        Assert.Equal(1, (int)RadarProcessingRetainedPayloadReleaseStatus.Released);
        Assert.Equal(2, (int)RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased);
        Assert.Equal(3, (int)RadarProcessingRetainedPayloadReleaseStatus.Failed);
        Assert.Equal(4, (int)RadarProcessingRetainedPayloadReleaseStatus.NotRequired);
    }

    [Fact]
    public void RetainedPayloadOptionsUseSnapshotCopyDefaultsAndValidateLimits()
    {
        var defaults = RadarProcessingRetainedPayloadOptions.Default;

        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, defaults.Strategy);
        Assert.Null(defaults.MaxRetainedPayloadBytes);
        Assert.False(defaults.HasMaxRetainedPayloadBytes);

        var explicitOptions = new RadarProcessingRetainedPayloadOptions(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            maxRetainedPayloadBytes: 4096);

        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, explicitOptions.Strategy);
        Assert.Equal(4096, explicitOptions.MaxRetainedPayloadBytes);
        Assert.True(explicitOptions.HasMaxRetainedPayloadBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadOptions((RadarProcessingRetainedPayloadStrategy)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadOptions(maxRetainedPayloadBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedPayloadOptions(maxRetainedPayloadBytes: -1));
    }

    [Fact]
    public void RetentionResultsSeparateSuccessAndRejectedStatuses()
    {
        var ownedBatch = CreateOwnedBatch();

        var succeeded = RadarProcessingRetainedPayloadRetentionResult.Succeeded(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            ownedBatch,
            TimeSpan.FromMilliseconds(3),
            allocatedBytes: 128);
        var unsupported = RadarProcessingRetainedPayloadRetentionResult.UnsupportedStrategy(
            RadarProcessingRetainedPayloadStrategy.BuilderTransfer,
            "not available");
        var failedCopy = RadarProcessingRetainedPayloadRetentionResult.FailedCopy(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            "copy failed");
        var canceled = RadarProcessingRetainedPayloadRetentionResult.Canceled(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy);
        var invalidInput = RadarProcessingRetainedPayloadRetentionResult.InvalidInput(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            "leased input missing");

        Assert.True(succeeded.IsSuccessful);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.Succeeded, succeeded.Status);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, succeeded.Strategy);
        Assert.Same(ownedBatch, succeeded.Batch);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, succeeded.Resource!.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, succeeded.Resource.Release().Status);
        Assert.Equal(TimeSpan.FromMilliseconds(3), succeeded.Elapsed);
        Assert.Equal(128, succeeded.AllocatedBytes);
        Assert.Equal(0, succeeded.PoolRentCount);
        Assert.Equal(0, succeeded.PoolMissCount);
        Assert.Equal(0, succeeded.EventPoolRentCount);
        Assert.Equal(0, succeeded.PayloadPoolRentCount);
        Assert.Equal(0, succeeded.EventPoolMissCount);
        Assert.Equal(0, succeeded.PayloadPoolMissCount);
        Assert.Equal(1, succeeded.EventCount);
        Assert.Equal(2, succeeded.PayloadBytes);
        Assert.Equal(2, succeeded.PayloadValueCount);
        Assert.Equal(15, succeeded.RawValueChecksum);

        var pooledSucceeded = RadarProcessingRetainedPayloadRetentionResult.Succeeded(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            ownedBatch,
            poolRentCount: 2,
            poolMissCount: 1,
            eventPoolRentCount: 1,
            payloadPoolRentCount: 1,
            eventPoolMissCount: 1);
        Assert.Equal(2, pooledSucceeded.PoolRentCount);
        Assert.Equal(1, pooledSucceeded.PoolMissCount);
        Assert.Equal(1, pooledSucceeded.EventPoolRentCount);
        Assert.Equal(1, pooledSucceeded.PayloadPoolRentCount);
        Assert.Equal(1, pooledSucceeded.EventPoolMissCount);
        Assert.Equal(0, pooledSucceeded.PayloadPoolMissCount);

        Assert.False(unsupported.IsSuccessful);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy, unsupported.Status);
        Assert.Null(unsupported.Batch);
        Assert.Equal("not available", unsupported.Message);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.FailedCopy, failedCopy.Status);
        Assert.Equal("copy failed", failedCopy.Message);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.Canceled, canceled.Status);
        Assert.Equal(RadarProcessingRetainedPayloadRetentionStatus.InvalidInput, invalidInput.Status);
        Assert.Equal("leased input missing", invalidInput.Message);

        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                (RadarProcessingRetainedPayloadStrategy)255,
                ownedBatch));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                ownedBatch,
                TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                ownedBatch,
                allocatedBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                ownedBatch,
                poolRentCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                ownedBatch,
                poolMissCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                ownedBatch,
                eventPoolRentCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                ownedBatch,
                payloadPoolMissCount: -1));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedPayloadRetentionResult.FailedCopy(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                null!));

        var builder = CreateSingleEventBuilder();
        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Throws<ArgumentException>(() =>
                RadarProcessingRetainedPayloadRetentionResult.Succeeded(
                    RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                    batch));
        });
    }
}
