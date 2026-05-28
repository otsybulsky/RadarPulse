using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingProviderQueueContractTests
{
    [Fact]
    public void ProviderQueueEnumValuesAreStable()
    {
        Assert.Equal(1, (int)RadarProcessingProviderQueueFullMode.ReturnFull);
        Assert.Equal(2, (int)RadarProcessingProviderQueueFullMode.Wait);

        Assert.Equal(1, (int)RadarProcessingProviderQueueShutdownMode.Drain);
        Assert.Equal(2, (int)RadarProcessingProviderQueueShutdownMode.CancelQueued);

        Assert.Equal(1, (int)RadarProcessingQueuedBatchEnqueueStatus.Accepted);
        Assert.Equal(2, (int)RadarProcessingQueuedBatchEnqueueStatus.Full);
        Assert.Equal(3, (int)RadarProcessingQueuedBatchEnqueueStatus.TimedOut);
        Assert.Equal(4, (int)RadarProcessingQueuedBatchEnqueueStatus.Canceled);
        Assert.Equal(5, (int)RadarProcessingQueuedBatchEnqueueStatus.Closed);
        Assert.Equal(6, (int)RadarProcessingQueuedBatchEnqueueStatus.Faulted);

        Assert.Equal(1, (int)RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        Assert.Equal(2, (int)RadarProcessingQueuedBatchProcessingStatus.FailedProcessing);
        Assert.Equal(3, (int)RadarProcessingQueuedBatchProcessingStatus.FailedValidation);
        Assert.Equal(4, (int)RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        Assert.Equal(5, (int)RadarProcessingQueuedBatchProcessingStatus.Canceled);
        Assert.Equal(6, (int)RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);

        Assert.Equal(0, (int)RadarProcessingQueuedSessionStatus.NotStarted);
        Assert.Equal(1, (int)RadarProcessingQueuedSessionStatus.Running);
        Assert.Equal(2, (int)RadarProcessingQueuedSessionStatus.Draining);
        Assert.Equal(3, (int)RadarProcessingQueuedSessionStatus.Completed);
        Assert.Equal(4, (int)RadarProcessingQueuedSessionStatus.Faulted);
        Assert.Equal(5, (int)RadarProcessingQueuedSessionStatus.Canceled);
        Assert.Equal(6, (int)RadarProcessingQueuedSessionStatus.Disposed);

        Assert.Equal(1, (int)RadarProcessingProviderQueueRecentDetailKind.Enqueue);
        Assert.Equal(2, (int)RadarProcessingProviderQueueRecentDetailKind.Dequeue);
        Assert.Equal(3, (int)RadarProcessingProviderQueueRecentDetailKind.Processing);
    }

    [Fact]
    public void ProviderQueueOptionsUseConservativeBoundedDefaults()
    {
        var options = RadarProcessingProviderQueueOptions.Default;

        Assert.Equal(1, options.Capacity);
        Assert.Equal(RadarProcessingProviderQueueFullMode.Wait, options.FullMode);
        Assert.Null(options.EnqueueTimeout);
        Assert.False(options.HasEnqueueTimeout);
        Assert.Equal(RadarProcessingProviderQueueShutdownMode.Drain, options.ShutdownMode);
        Assert.Equal(16, options.RecentDetailCapacity);
        Assert.Null(options.MaxRetainedPayloadBytes);
        Assert.False(options.HasMaxRetainedPayloadBytes);
    }

    [Fact]
    public void ProviderQueueOptionsComposeExplicitSettings()
    {
        var timeout = TimeSpan.FromMilliseconds(250);

        var options = new RadarProcessingProviderQueueOptions(
            capacity: 4,
            fullMode: RadarProcessingProviderQueueFullMode.Wait,
            enqueueTimeout: timeout,
            shutdownMode: RadarProcessingProviderQueueShutdownMode.CancelQueued,
            recentDetailCapacity: 3,
            maxRetainedPayloadBytes: 4096);

        Assert.Equal(4, options.Capacity);
        Assert.Equal(RadarProcessingProviderQueueFullMode.Wait, options.FullMode);
        Assert.Equal(timeout, options.EnqueueTimeout);
        Assert.True(options.HasEnqueueTimeout);
        Assert.Equal(RadarProcessingProviderQueueShutdownMode.CancelQueued, options.ShutdownMode);
        Assert.Equal(3, options.RecentDetailCapacity);
        Assert.Equal(4096, options.MaxRetainedPayloadBytes);
        Assert.True(options.HasMaxRetainedPayloadBytes);
    }

    [Fact]
    public void ProviderQueueOptionsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(fullMode: (RadarProcessingProviderQueueFullMode)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(shutdownMode: (RadarProcessingProviderQueueShutdownMode)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(recentDetailCapacity: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(maxRetainedPayloadBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(maxRetainedPayloadBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueOptions(enqueueTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingProviderQueueOptions(
                fullMode: RadarProcessingProviderQueueFullMode.ReturnFull,
                enqueueTimeout: TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void QueuedBatchSequenceIsMonotonicAndRejectsNegativeValues()
    {
        var sequence = RadarProcessingQueuedBatchSequence.Initial;

        Assert.Equal(0, sequence.Value);
        Assert.Equal(1, sequence.Next().Value);
        Assert.Equal("0", sequence.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadarProcessingQueuedBatchSequence(-1));
    }

    [Fact]
    public void QueuedBatchRequiresOwnedPayloadAndCarriesSnapshotCost()
    {
        var ownedBatch = CreateOwnedBatch();
        var queued = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(7),
            ownedBatch,
            ownedSnapshotTime: TimeSpan.FromMilliseconds(2),
            ownedSnapshotAllocatedBytes: 64);

        Assert.Equal(7, queued.Sequence.Value);
        Assert.Same(ownedBatch, queued.Batch);
        Assert.Equal(TimeSpan.FromMilliseconds(2), queued.OwnedSnapshotTime);
        Assert.Equal(64, queued.OwnedSnapshotAllocatedBytes);
        Assert.Equal(0, queued.EnqueuedTimestamp);
        Assert.Equal(1, queued.StreamEventCount);
        Assert.Equal(2, queued.PayloadBytes);
        Assert.Equal(2, queued.PayloadValueCount);
        Assert.Equal(15, queued.RawValueChecksum);

        var builder = CreateSingleEventBuilder();
        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedBatch(RadarProcessingQueuedBatchSequence.Initial, batch));
        });

        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingQueuedBatch(RadarProcessingQueuedBatchSequence.Initial, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
                ownedBatch,
                ownedSnapshotTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
                ownedBatch,
                ownedSnapshotAllocatedBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
                ownedBatch,
                enqueuedTimestamp: -1));
    }
}
