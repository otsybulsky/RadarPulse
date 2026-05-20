using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
    [Fact]
    public async Task PublishCopiesLeasedBatchToOwnedQueueBeforeCallbackReturns()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        var builder = CreateBuilder([1, 2]);
        var callbackObservedEnqueuedOwnedBatch = false;

        builder.ConsumeLeased(batch =>
        {
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            publisher.Publish(batch, CancellationToken.None);
            callbackObservedEnqueuedOwnedBatch = queue.PendingCount == 1;
        });
        AddEvent(builder, [9, 10]);

        var dequeue = await queue.DequeueAsync();
        var queued = dequeue.Batch!;
        Assert.True(callbackObservedEnqueuedOwnedBatch);
        Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);
        Assert.Equal(RadarEventBatchLifetime.Owned, queued.Batch.Lifetime);
        Assert.Equal([1, 2], queued.Batch.Payload.ToArray());
        Assert.True(queued.Batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum));
        Assert.Equal(2, payloadValueCount);
        Assert.Equal(3, rawValueChecksum);
        Assert.Single(publisher.EnqueueResults);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Accepted, publisher.EnqueueResults[0].Status);
        Assert.Equal(1, publisher.CreateResult().Telemetry.OwnedSnapshotCount);
        Assert.True(publisher.CreateResult().Telemetry.TotalOwnedSnapshotTime >= TimeSpan.Zero);
    }

    [Fact]
    public void PublishRecordsBackpressureResultAndThrowsWhenQueueReturnsFull()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(
                capacity: 1,
                fullMode: RadarProcessingProviderQueueFullMode.ReturnFull));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);

        PublishLeased(publisher, [1]);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PublishLeased(publisher, [2]));
        var result = publisher.CreateResult();

        Assert.Contains("Full", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, result.PublishAttemptCount);
        Assert.Equal(1, result.AcceptedPublishCount);
        Assert.Equal(1, result.RejectedPublishCount);
        Assert.True(result.HasRejectedPublish);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Full, result.LastEnqueueResult?.Status);
        Assert.Equal(1, result.Telemetry.EnqueuedBatchCount);
        Assert.Equal(1, result.Telemetry.EnqueueFullCount);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public void ProcessingFaultCausesLaterPublishToFailWithoutEnqueueingPartialWork()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        PublishLeased(publisher, [1]);

        queue.Fault("processing failed");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PublishLeased(publisher, [2]));
        var result = publisher.CreateResult();

        Assert.Contains("Faulted", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, result.PublishAttemptCount);
        Assert.Equal(1, result.AcceptedPublishCount);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, result.LastEnqueueResult?.Status);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public void CancellationDuringPublishDoesNotEnqueuePartialWork()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            PublishLeased(publisher, [1], cancellation.Token));
        var result = publisher.CreateResult();

        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(1, result.PublishAttemptCount);
        Assert.Equal(0, result.AcceptedPublishCount);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Canceled, result.LastEnqueueResult?.Status);
    }

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
    public async Task ConsumerResourcePressureUsesBatchPayloadBytesWhenReleaseIsNotRequired()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(queue);

        PublishLeased(publisher, [1, 2, 3]);

        var dequeue = await queue.DequeueAsync();
        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var activeTelemetry = publisher.CreateResult().Telemetry;

        Assert.Equal(1, activeTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(3, activeTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(3, activeTelemetry.ActiveRetainedPayloadBytesHighWatermark);

        var release = lease.Release();
        var released = publisher.CreateResult();

        Assert.Equal(RadarProcessingRetainedPayloadReleaseStatus.NotRequired, release.Status);
        Assert.Equal(0, released.Telemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, released.RetentionTelemetry.ReleaseNotRequiredCount);
    }

    private static void PublishLeased(
        ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        var builder = CreateBuilder(payload);
        builder.ConsumeLeased(batch => publisher.Publish(batch, cancellationToken));
    }

    private static RadarEventBatchBuilder CreateBuilder(byte[] payload)
    {
        var builder = new RadarEventBatchBuilder(initialEventCapacity: 1, initialPayloadCapacity: payload.Length);
        AddEvent(builder, payload);
        return builder;
    }

    private static void AddEvent(
        RadarEventBatchBuilder builder,
        byte[] payload)
    {
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
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks: 100,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: 1,
            gateStart: 0,
            gateCount: (ushort)payload.Length,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payload: payload);
    }
}
