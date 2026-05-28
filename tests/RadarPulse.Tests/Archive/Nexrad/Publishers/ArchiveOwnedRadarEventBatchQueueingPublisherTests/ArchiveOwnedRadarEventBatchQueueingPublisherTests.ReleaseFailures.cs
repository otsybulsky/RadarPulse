using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
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

    [Fact]
    public void ReleasePendingResourcesRecordsReleaseFailureAndClearsPendingPressure()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                new ThrowingReturnArrayPool<byte>()));

        PublishLeased(publisher, [1, 2, 3]);
        var pending = publisher.CreateResult();

        Assert.Equal(1, pending.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(3, pending.Telemetry.CurrentPendingRetainedPayloadBytes);

        var cleanup = publisher.ReleasePendingResources();
        var result = publisher.CreateResult();
        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            result.RetentionTelemetry);

        Assert.False(cleanup.IsSuccessful);
        Assert.Equal(1, cleanup.FailedCount);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed, readiness.Error);
    }

    [Fact]
    public async Task ConsumerReleaseFailureRecordsReadinessFailureAndClearsActivePressure()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                new ThrowingReturnArrayPool<byte>()));

        PublishLeased(publisher, [1, 2, 3]);
        var dequeue = await queue.DequeueAsync();

        using var lease = publisher.AcquireConsumerResourceLease(dequeue.Batch!.Sequence);
        var active = publisher.CreateResult();

        Assert.Equal(0, active.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(1, active.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(3, active.Telemetry.CurrentActiveRetainedPayloadBytes);

        var release = lease.Release();
        var result = publisher.CreateResult();
        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            result.RetentionTelemetry);

        Assert.True(release.IsFailure);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(0, result.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(1, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed, readiness.Error);
    }
}
