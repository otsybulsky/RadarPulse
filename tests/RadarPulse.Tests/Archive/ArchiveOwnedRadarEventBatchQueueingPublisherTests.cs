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
