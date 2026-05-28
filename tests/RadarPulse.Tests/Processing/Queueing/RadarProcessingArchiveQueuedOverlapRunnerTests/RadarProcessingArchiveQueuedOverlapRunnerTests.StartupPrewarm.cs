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
    public async Task OmittedOptionsApplyRuntimeDefaultStartupPrewarm()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2, 3], cancellationToken);
                return CreatePublishResult(batchCount: 1);
            },
            async (queue, publisher, cancellationToken) =>
            {
                var dequeue = await queue.DequeueAsync(cancellationToken);
                Assert.Equal(RadarProcessingOwnedBatchDequeueStatus.Item, dequeue.Status);
                using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);

                return CreateSessionResult(
                    queue,
                    [
                        RadarProcessingQueuedBatchProcessingResult.Succeeded(
                            dequeue.Batch.Sequence,
                            CreateProcessingResult())
                    ]);
            });

        Assert.True(result.IsCompleted);
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            result.RetainedPayloadPrewarm.EventCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            result.RetainedPayloadPrewarm.PayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.RetainedPayloadPrewarm.AllocatedBytes > 0);
        Assert.True(result.RetainedPayloadPrewarm.RetainedBytes > 0);
        Assert.True(result.OverlapTelemetry.RetentionAllocatedBytes < result.RetainedPayloadPrewarm.AllocatedBytes);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
    }

    [Fact]
    public async Task ExplicitOptionsDoNotApplyStartupPrewarmUnlessRequested()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(CreateOwnedBatch(1), cancellationToken);
                return CreatePublishResult(batchCount: 1);
            },
            DrainAllAsync,
            options);

        Assert.True(result.IsCompleted);
        Assert.False(result.HasRetainedPayloadPrewarm);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.OverlapTelemetry.RetentionStrategy);
    }

}
