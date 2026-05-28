using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
    [Fact]
    public async Task ConsumerResourceLeaseMovesPressureFromPendingToActiveUntilRelease()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy));

        PublishLeased(publisher, [1, 2]);
        PublishLeased(publisher, [3, 4, 5]);

        var dequeue = await queue.DequeueAsync();
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);

        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var activeTelemetry = publisher.CreateResult().Telemetry;

        Assert.Equal(1, activeTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(3, activeTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(2, activeTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(5, activeTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, activeTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(2, activeTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, activeTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(2, activeTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(2, activeTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(5, activeTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(2, activeTelemetry.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(5, activeTelemetry.CombinedRetainedPayloadBytesHighWatermark);

        var release = lease.Release();
        var releasedTelemetry = publisher.CreateResult().Telemetry;

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, release.Status);
        Assert.Equal(1, releasedTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(3, releasedTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, releasedTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, releasedTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, releasedTelemetry.ActiveRetainedBatchCountHighWatermark);

        var cleanup = publisher.ReleasePendingResources();
        var cleanedTelemetry = publisher.CreateResult().Telemetry;

        Assert.Single(cleanup.ReleaseResults);
        Assert.Equal(0, cleanedTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, cleanedTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(2, publisher.CreateResult().RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(2, publisher.CreateResult().RetentionTelemetry.ReleasedBatchCount);
    }

    [Fact]
    public async Task WaitingConsumerCanAcquireRetainedResourceForAcceptedPublish()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy));

        var consumer = Task.Run(async () =>
        {
            var dequeue = await queue.DequeueAsync();
            Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);

            using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
            return publisher.CreateResult().Telemetry;
        });

        await Task.Delay(50);
        PublishLeased(publisher, [1, 2, 3]);
        var telemetry = await consumer.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(1, telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(3, telemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(1, telemetry.ActiveRetainedBatchCountHighWatermark);
    }

    [Fact]
    public async Task PooledCopyRetentionTelemetryAggregatesPoolCounters()
    {
        var payloadPool = new RadarProcessingRetainedPayloadByteArrayPool(
            largeArrayThreshold: 4,
            maxRetainedArrayCount: 2,
            maxRetainedBytes: 16);
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                payloadPool));

        PublishLeased(publisher, [1, 2, 3, 4, 5]);
        var published = publisher.CreateResult();

        Assert.Equal(2, published.RetentionTelemetry.PoolRentCount);
        Assert.Equal(1, published.RetentionTelemetry.PoolMissCount);
        Assert.Equal(0, published.RetentionTelemetry.PoolReturnCount);
        Assert.Equal(1, published.RetentionTelemetry.EventPoolRentCount);
        Assert.Equal(0, published.RetentionTelemetry.EventPoolMissCount);
        Assert.Equal(1, published.RetentionTelemetry.PayloadPoolRentCount);
        Assert.Equal(1, published.RetentionTelemetry.PayloadPoolMissCount);

        var dequeue = await queue.DequeueAsync();
        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var release = lease.Release();
        var released = publisher.CreateResult();

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.Released, release.Status);
        Assert.Equal(2, release.PoolReturnCount);
        Assert.Equal(1, release.EventPoolReturnCount);
        Assert.Equal(1, release.PayloadPoolReturnCount);
        Assert.Equal(2, released.RetentionTelemetry.PoolReturnCount);
        Assert.Equal(1, released.RetentionTelemetry.EventPoolReturnCount);
        Assert.Equal(1, released.RetentionTelemetry.PayloadPoolReturnCount);
        Assert.Equal(1, payloadPool.RetainedArrayCount);
        Assert.Equal(8, payloadPool.RetainedBytes);
    }
}
