using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRetainedPayloadContractTests
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
        Assert.Equal(TimeSpan.FromMilliseconds(3), succeeded.Elapsed);
        Assert.Equal(128, succeeded.AllocatedBytes);
        Assert.Equal(1, succeeded.EventCount);
        Assert.Equal(2, succeeded.PayloadBytes);
        Assert.Equal(2, succeeded.PayloadValueCount);
        Assert.Equal(15, succeeded.RawValueChecksum);

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

    [Fact]
    public void ReleaseResultsSeparateLifecycleStatuses()
    {
        var released = RadarProcessingRetainedPayloadReleaseResult.Released(
            RadarProcessingRetainedPayloadStrategy.PooledCopy,
            TimeSpan.FromMilliseconds(2),
            payloadBytes: 64);
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
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingRetainedPayloadReleaseResult.Failed(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                null!));
    }

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
            totalReleaseTime: TimeSpan.FromMilliseconds(2));

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

    private static RadarEventBatch CreateOwnedBatch() =>
        CreateSingleEventBuilder().Build();

    private static RadarEventBatchBuilder CreateSingleEventBuilder()
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: 2);
        builder.AddEvent(
            new RadarStreamIdentity(
                sourceId: 0,
                radarOrdinal: 0,
                momentId: 0,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                dictionaryVersion: DictionaryVersion.Initial,
                sourceUniverseVersion: SourceUniverseVersion.Initial),
            volumeTimestampUtcTicks: 1,
            messageTimestampUtcTicks: 2,
            sourceRecord: 0,
            sourceMessage: 0,
            radialSequence: 0,
            gateStart: 0,
            gateCount: 2,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: [7, 8]);

        return builder;
    }
}
