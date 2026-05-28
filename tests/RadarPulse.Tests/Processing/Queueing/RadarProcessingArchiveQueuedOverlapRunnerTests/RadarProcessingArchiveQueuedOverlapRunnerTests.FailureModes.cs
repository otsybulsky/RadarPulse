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
    public async Task ConsumerFailureStopsProducerIntakeAndFaultsOverlap()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        using var consumerFaulted = new ManualResetEventSlim();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                fullMode: RadarProcessingProviderQueueFullMode.Wait,
                enqueueTimeout: TimeSpan.FromSeconds(5),
                recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateOwnedBatch(1), cancellationToken);
                Assert.True(consumerFaulted.Wait(TimeSpan.FromSeconds(5)));
                publisher.Publish(CreateOwnedBatch(3), cancellationToken);
                return CreatePublishResult(batchCount: 2);
            },
            async (queue, cancellationToken) =>
            {
                var first = await queue.DequeueAsync(cancellationToken);
                Assert.True(first.HasItem);

                const string failure = "processing failed";
                queue.Fault(failure);
                consumerFaulted.Set();
                return CreateSessionResult(
                    queue,
                    [
                        RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                            first.Batch!.Sequence,
                            failure)
                    ],
                    RadarProcessingQueuedSessionStatus.Faulted,
                    failure);
            },
            options);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.True(result.Producer.IsFailed);
        Assert.Equal("processing failed", result.Message);
        Assert.Equal(2, result.ProviderResult.PublishAttemptCount);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Single(result.Consumer.SessionResult.ProcessingResults);
    }

    [Fact]
    public async Task ProducerFailureReleasesPendingResourcesAndFaultsOverlap()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken);
                throw new InvalidOperationException("producer failed");
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken);
                return CreateSessionResult(
                    queue,
                    Array.Empty<RadarProcessingQueuedBatchProcessingResult>());
            },
            options);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Producer.IsFailed);
        Assert.Equal("producer failed", result.Message);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.QueueTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(2, result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
    }

}
