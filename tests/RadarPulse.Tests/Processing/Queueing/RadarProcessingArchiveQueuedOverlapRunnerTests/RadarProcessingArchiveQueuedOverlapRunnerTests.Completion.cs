using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunnerTests
{
    [Fact]
    public async Task OverlapRunnerLetsProducerQueueAheadWhileConsumerDrainsInOrder()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 4, recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateOwnedBatch(1), cancellationToken);
                publisher.Publish(CreateOwnedBatch(3), cancellationToken);
                publisher.Publish(CreateOwnedBatch(5), cancellationToken);
                return CreatePublishResult(batchCount: 3);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken);
                return await DrainAllAsync(queue, cancellationToken);
            },
            options);

        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.Completed, result.Status);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsCompleted);
        Assert.Equal(3, result.Producer.PublishResult!.BatchCount);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(3, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(3, result.QueueTelemetry.OwnedSnapshotEventCount);
        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal([0L, 1L, 2L], result.Consumer.SessionResult.ProcessingResults
            .Select(static item => item.Sequence.Value)
            .ToArray());
        Assert.True(result.QueueTelemetry.QueueDepthHighWatermark > 1);

        var overlapTelemetry = result.OverlapTelemetry;
        Assert.Equal(result.QueueTelemetry, overlapTelemetry.QueueTelemetry);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, overlapTelemetry.RetentionStrategy);
        Assert.Equal(result.Elapsed, overlapTelemetry.Elapsed);
        Assert.Equal(result.Producer.Elapsed, overlapTelemetry.ProducerActiveTime);
        Assert.Equal(result.Consumer.Elapsed, overlapTelemetry.ConsumerActiveTime);
        Assert.True(overlapTelemetry.HasProducerConsumerOverlap);
        Assert.True(overlapTelemetry.HasQueuedAheadOverlap);
        Assert.Equal(3, overlapTelemetry.RetainedBatchCount);
        Assert.Equal(3, overlapTelemetry.RetainedEventCount);
        Assert.Equal(6, overlapTelemetry.RetainedPayloadBytes);
        Assert.Equal(6, overlapTelemetry.RetainedPayloadValueCount);
        Assert.Equal(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, overlapTelemetry.RetentionAllocatedBytes);
        Assert.Equal(result.QueueTelemetry.TotalOwnedSnapshotTime, overlapTelemetry.TotalRetentionTime);
        Assert.Equal(result.QueueTelemetry.TotalProviderToProcessingLatency, overlapTelemetry.TotalProviderToProcessingLatency);
        Assert.Equal(result.QueueTelemetry.TotalEnqueueWaitTime, overlapTelemetry.ProviderBlockedTime);
        Assert.Equal(result.QueueTelemetry.TotalDequeueWaitTime, overlapTelemetry.ConsumerIdleTime);
        Assert.Same(result.QueueTelemetry.RetainedResourcePressure, overlapTelemetry.RetainedResourcePressure);
        Assert.Equal(result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark, overlapTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark, overlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark, overlapTelemetry.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Equal(3, overlapTelemetry.ReleaseAttemptCount);
        Assert.Equal(3, overlapTelemetry.ReleaseNotRequiredCount);
    }

}
