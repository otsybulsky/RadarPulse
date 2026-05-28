using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisherTests
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
}
