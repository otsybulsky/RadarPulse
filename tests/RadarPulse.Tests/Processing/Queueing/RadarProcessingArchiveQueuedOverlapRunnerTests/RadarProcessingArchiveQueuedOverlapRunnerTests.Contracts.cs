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
    public void ArchiveQueuedOverlapContractsExposeStableStatusesAndDefaults()
    {
        Assert.Equal(0, (int)RadarProcessingArchiveQueuedOverlapStatus.NotStarted);
        Assert.Equal(1, (int)RadarProcessingArchiveQueuedOverlapStatus.Completed);
        Assert.Equal(2, (int)RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed);
        Assert.Equal(3, (int)RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted);
        Assert.Equal(4, (int)RadarProcessingArchiveQueuedOverlapStatus.Canceled);
        Assert.Equal(5, (int)RadarProcessingArchiveQueuedOverlapStatus.Disposed);

        Assert.Equal(0, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.NotStarted);
        Assert.Equal(1, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.Completed);
        Assert.Equal(2, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.Failed);
        Assert.Equal(3, (int)RadarProcessingArchiveQueuedOverlapProducerStatus.Canceled);

        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
            RadarProcessingArchiveQueuedOverlapOptions.Default.QueueOptions.Capacity);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            RadarProcessingArchiveQueuedOverlapOptions.Default.QueueOptions.MaxRetainedPayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
            RadarProcessingArchiveQueuedOverlapOptions.Default.RetainedPayloadOptions.Strategy);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes,
            RadarProcessingArchiveQueuedOverlapOptions.Default.RetainedPayloadOptions.MaxRetainedPayloadBytes);
        Assert.Equal(
            RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault,
            RadarProcessingArchiveQueuedOverlapOptions.Default.RetainedPayloadPrewarmOptions);
        Assert.True(RadarProcessingArchiveQueuedOverlapOptions.Default.IsRuntimeDefaultContour);
        Assert.Null(RadarProcessingArchiveQueuedOverlapOptions.Default.RetainedPayloadFactory);

        var explicitDiagnosticOptions = new RadarProcessingArchiveQueuedOverlapOptions();
        Assert.Same(RadarProcessingProviderQueueOptions.Default, explicitDiagnosticOptions.QueueOptions);
        Assert.Same(RadarProcessingRetainedPayloadOptions.Default, explicitDiagnosticOptions.RetainedPayloadOptions);
        Assert.Equal(RadarProcessingRetainedPayloadPrewarmOptions.None, explicitDiagnosticOptions.RetainedPayloadPrewarmOptions);
        Assert.False(explicitDiagnosticOptions.IsRuntimeDefaultContour);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingArchiveQueuedOverlapResult(
                (RadarProcessingArchiveQueuedOverlapStatus)255,
                RadarProcessingArchiveQueuedOverlapProducerResult.Canceled(),
                RadarProcessingArchiveQueuedOverlapConsumerResult.Canceled()));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingArchiveQueuedOverlapProducerResult.Completed(
                null!,
                new RadarProcessingArchiveQueuedProviderResult()));
        Assert.Throws<ArgumentNullException>(() =>
            RadarProcessingArchiveQueuedOverlapProducerResult.Failed(null!));

        var pressure = new RadarProcessingRetainedResourcePressureSummary(
            pendingRetainedBatchCountHighWatermark: 2,
            pendingRetainedPayloadBytesHighWatermark: 4,
            activeRetainedBatchCountHighWatermark: 1,
            activeRetainedPayloadBytesHighWatermark: 3,
            combinedRetainedBatchCountHighWatermark: 3,
            combinedRetainedPayloadBytesHighWatermark: 7);
        var queueTelemetry = new RadarProcessingProviderQueueTelemetrySummary(
            ownedSnapshotCount: 2,
            ownedSnapshotPayloadBytes: 4,
            ownedSnapshotAllocatedBytes: 128,
            totalOwnedSnapshotTime: TimeSpan.FromMilliseconds(1),
            totalEnqueueWaitTime: TimeSpan.FromMilliseconds(2),
            queueDepthHighWatermark: 2,
            queuedPayloadBytesHighWatermark: 4,
            totalDequeueWaitTime: TimeSpan.FromMilliseconds(3),
            ownedSnapshotPayloadValueCount: 4,
            ownedSnapshotEventCount: 2,
            retainedResourcePressure: pressure);
        var retentionTelemetry = new RadarProcessingRetainedPayloadTelemetrySummary(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            retentionAttemptCount: 2,
            retainedBatchCount: 2,
            retainedEventCount: 2,
            retainedPayloadBytes: 4,
            retainedPayloadValueCount: 4,
            allocatedBytes: 128,
            totalRetentionTime: TimeSpan.FromMilliseconds(1),
            releaseAttemptCount: 2,
            releaseNotRequiredCount: 2);
        var overlap = new RadarProcessingArchiveOverlapTelemetrySummary(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            elapsed: TimeSpan.FromMilliseconds(10),
            producerActiveTime: TimeSpan.FromMilliseconds(6),
            consumerActiveTime: TimeSpan.FromMilliseconds(7),
            overlapElapsed: TimeSpan.FromMilliseconds(6),
            measuredAllocatedBytes: 256,
            queueTelemetry,
            retentionTelemetry);

        Assert.True(overlap.HasProducerConsumerOverlap);
        Assert.True(overlap.HasQueuedAheadOverlap);
        Assert.Equal(2, overlap.RetainedEventCount);
        Assert.Equal(4, overlap.RetainedPayloadBytes);
        Assert.Equal(128, overlap.RetentionAllocatedBytes);
        Assert.Same(pressure, overlap.RetainedResourcePressure);
        Assert.Equal(4, overlap.RetainedPayloadBytesHighWatermark);
        Assert.Equal(2, overlap.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(4, overlap.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, overlap.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(3, overlap.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(3, overlap.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(7, overlap.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Equal(TimeSpan.FromMilliseconds(2), overlap.ProviderBlockedTime);
        Assert.Equal(TimeSpan.FromMilliseconds(2), overlap.ProducerBlockedTime);
        Assert.Equal(TimeSpan.FromMilliseconds(3), overlap.ConsumerIdleTime);
        Assert.Equal(128, overlap.UnattributedAllocatedBytes);
        Assert.Equal(2, overlap.ReleaseNotRequiredCount);

        var pressureOnly = new RadarProcessingArchiveOverlapTelemetrySummary(
            retainedResourcePressure: pressure);
        Assert.Same(pressure, pressureOnly.RetainedResourcePressure);

        var completed = new RadarProcessingArchiveQueuedOverlapResult(
            RadarProcessingArchiveQueuedOverlapStatus.Completed,
            RadarProcessingArchiveQueuedOverlapProducerResult.Canceled(),
            RadarProcessingArchiveQueuedOverlapConsumerResult.Canceled());

        Assert.Same(RadarProcessingArchiveOverlapTelemetrySummary.Empty, completed.OverlapTelemetry);
        Assert.Same(completed.OverlapTelemetry, completed.Telemetry);
        Assert.False(completed.HasRetainedPayloadPrewarm);
        Assert.Equal(TimeSpan.Zero, RadarProcessingArchiveOverlapTelemetrySummary.Empty.OverlapElapsed);
        Assert.Equal(0, RadarProcessingArchiveOverlapTelemetrySummary.Empty.RetainedBatchCount);
        Assert.Equal(0, RadarProcessingArchiveOverlapTelemetrySummary.Empty.UnattributedAllocatedBytes);

        Assert.Throws<InvalidOperationException>(() =>
            new RadarProcessingArchiveQueuedOverlapOptions(
                retainedPayloadPrewarmOptions: RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingRetainedPayloadPrewarmOptions(eventCount: 1, payloadBytes: 0, retainedBatchCount: 1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingArchiveOverlapTelemetrySummary(
                producerActiveTime: TimeSpan.FromMilliseconds(1),
                consumerActiveTime: TimeSpan.FromMilliseconds(1),
                overlapElapsed: TimeSpan.FromMilliseconds(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingArchiveOverlapTelemetrySummary(elapsed: TimeSpan.FromTicks(-1)));
    }

}
