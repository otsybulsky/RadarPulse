using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisherTests
{
    [Fact]
    public void RetentionFailureStopsCurrentPublishAndLeavesAcceptedResourcesForTerminalCleanup()
    {
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 2));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            retainedPayloadFactory: new RadarProcessingRetainedPayloadFactory(
                ArrayPool<RadarStreamEvent>.Shared,
                new FailingRentArrayPool<byte>(successfulRentCount: 1)));

        PublishLeased(publisher, [1, 2]);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PublishLeased(publisher, [3, 4]));
        var rejected = publisher.CreateResult();

        Assert.Contains("FailedCopy", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, rejected.PublishAttemptCount);
        Assert.Equal(1, rejected.AcceptedPublishCount);
        Assert.Equal(2, rejected.RetentionTelemetry.RetentionAttemptCount);
        Assert.Equal(1, rejected.RetentionTelemetry.RetentionFailedCopyCount);
        Assert.Equal(1, rejected.Telemetry.CurrentPendingRetainedBatchCount);

        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            rejected.RetentionTelemetry);
        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceRetentionFailed, readiness.Error);
        Assert.Equal(0, readiness.ExpectedCount);
        Assert.Equal(1, readiness.ActualCount);

        var cleanup = publisher.ReleasePendingResources();
        var cleaned = publisher.CreateResult();

        Assert.True(cleanup.IsSuccessful);
        Assert.Equal(1, cleanup.ReleasedCount);
        Assert.Equal(0, cleaned.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, cleaned.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, cleaned.RetentionTelemetry.ReleasedBatchCount);
    }
}
