using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingProviderQueueContractTests
{
    [Fact]
    public void ProviderQueueTelemetrySummaryCarriesCountersAndRejectsInvalidShapes()
    {
        var summary = new RadarProcessingProviderQueueTelemetrySummary(
            ownedSnapshotCount: 2,
            ownedSnapshotPayloadBytes: 128,
            ownedSnapshotAllocatedBytes: 256,
            totalOwnedSnapshotTime: TimeSpan.FromMilliseconds(3),
            enqueueAttemptCount: 3,
            enqueuedBatchCount: 2,
            enqueueFullCount: 1,
            enqueueTimedOutCount: 1,
            totalEnqueueWaitTime: TimeSpan.FromMilliseconds(5),
            dequeuedBatchCount: 2,
            completedBatchCount: 1,
            failedBatchCount: 1,
            totalDrainTime: TimeSpan.FromMilliseconds(7),
            queueDepthHighWatermark: 2,
            queuedPayloadBytesHighWatermark: 128,
            ownedSnapshotPayloadValueCount: 64,
            totalProviderToProcessingLatency: TimeSpan.FromMilliseconds(11),
            totalDequeueWaitTime: TimeSpan.FromMilliseconds(13),
            recentDetails:
            [
                new RadarProcessingProviderQueueRecentDetail(
                    RadarProcessingProviderQueueRecentDetailKind.Enqueue,
                    RadarProcessingQueuedBatchSequence.Initial,
                    enqueueStatus: RadarProcessingQueuedBatchEnqueueStatus.Accepted,
                    payloadBytes: 64,
                    payloadValueCount: 32)
            ],
            droppedRecentDetailCount: 3,
            ownedSnapshotEventCount: 5);

        Assert.Equal(2, summary.OwnedSnapshotCount);
        Assert.Equal(128, summary.OwnedSnapshotPayloadBytes);
        Assert.Equal(5, summary.OwnedSnapshotEventCount);
        Assert.Equal(64, summary.OwnedSnapshotPayloadValueCount);
        Assert.Equal(256, summary.OwnedSnapshotAllocatedBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(3), summary.TotalOwnedSnapshotTime);
        Assert.Equal(3, summary.EnqueueAttemptCount);
        Assert.Equal(2, summary.EnqueuedBatchCount);
        Assert.Equal(1, summary.EnqueueFullCount);
        Assert.Equal(1, summary.EnqueueTimedOutCount);
        Assert.Equal(TimeSpan.FromMilliseconds(5), summary.TotalEnqueueWaitTime);
        Assert.Equal(TimeSpan.FromMilliseconds(13), summary.TotalDequeueWaitTime);
        Assert.Equal(2, summary.DequeuedBatchCount);
        Assert.Equal(1, summary.CompletedBatchCount);
        Assert.Equal(1, summary.FailedBatchCount);
        Assert.Equal(TimeSpan.FromMilliseconds(7), summary.TotalDrainTime);
        Assert.Equal(2, summary.QueueDepthHighWatermark);
        Assert.Equal(128, summary.QueuedPayloadBytesHighWatermark);
        Assert.Equal(128, summary.RetainedPayloadBytesHighWatermark);
        Assert.Equal(0, summary.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, summary.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(2, summary.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(128, summary.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(0, summary.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, summary.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, summary.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(0, summary.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(0, summary.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, summary.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(2, summary.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(128, summary.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Equal(128, summary.RetainedResourcePressure.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(TimeSpan.FromMilliseconds(11), summary.TotalProviderToProcessingLatency);
        Assert.Single(summary.RecentDetails);
        Assert.Equal(1, summary.RetainedRecentDetailCount);
        Assert.Equal(3, summary.DroppedRecentDetailCount);
        Assert.Equal(4.0, summary.OwnedSnapshotAllocation.AllocatedBytesPerPayloadValue);
        Assert.True(summary.HasBackpressure);
        Assert.False(RadarProcessingProviderQueueTelemetrySummary.Empty.HasBackpressure);
        Assert.Same(RadarProcessingOwnedSnapshotAllocationSummary.Empty, RadarProcessingOwnedSnapshotAllocationSummary.Empty);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(ownedSnapshotCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(totalOwnedSnapshotTime: TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(
                enqueueAttemptCount: 1,
                enqueuedBatchCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(
                dequeuedBatchCount: 1,
                completedBatchCount: 1,
                failedBatchCount: 1));
        var canceledBeforeDequeue = new RadarProcessingProviderQueueTelemetrySummary(
            enqueueAttemptCount: 1,
            enqueuedBatchCount: 1,
            canceledBatchCount: 1);
        Assert.Equal(1, canceledBeforeDequeue.CanceledBatchCount);
        Assert.Equal(0, canceledBeforeDequeue.DequeuedBatchCount);
        Assert.Throws<ArgumentNullException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(
                recentDetails: new RadarProcessingProviderQueueRecentDetail[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(droppedRecentDetailCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(ownedSnapshotEventCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingProviderQueueTelemetrySummary(totalDequeueWaitTime: TimeSpan.FromTicks(-1)));

        var pressure = new RadarProcessingRetainedResourcePressureSummary(
            currentPendingRetainedBatchCount: 1,
            currentPendingRetainedPayloadBytes: 64,
            pendingRetainedBatchCountHighWatermark: 2,
            pendingRetainedPayloadBytesHighWatermark: 128,
            activeRetainedBatchCountHighWatermark: 0,
            activeRetainedPayloadBytesHighWatermark: 0,
            combinedRetainedBatchCountHighWatermark: 2,
            combinedRetainedPayloadBytesHighWatermark: 128);
        var explicitPressure = new RadarProcessingProviderQueueTelemetrySummary(
            queueDepthHighWatermark: 2,
            queuedPayloadBytesHighWatermark: 128,
            retainedResourcePressure: pressure);

        Assert.Same(pressure, explicitPressure.RetainedResourcePressure);
        Assert.Equal(1, explicitPressure.CurrentPendingRetainedBatchCount);
        Assert.Equal(64, explicitPressure.CurrentPendingRetainedPayloadBytes);

        var activePressure = new RadarProcessingRetainedResourcePressureSummary(
            currentActiveRetainedBatchCount: 1,
            currentActiveRetainedPayloadBytes: 32,
            activeRetainedBatchCountHighWatermark: 1,
            activeRetainedPayloadBytesHighWatermark: 32,
            combinedRetainedBatchCountHighWatermark: 1,
            combinedRetainedPayloadBytesHighWatermark: 32);
        var updatedPressure = explicitPressure.WithRetainedResourcePressure(activePressure);

        Assert.Equal(explicitPressure.EnqueuedBatchCount, updatedPressure.EnqueuedBatchCount);
        Assert.Same(activePressure, updatedPressure.RetainedResourcePressure);
        Assert.Equal(1, updatedPressure.CurrentActiveRetainedBatchCount);
        Assert.Equal(32, updatedPressure.CurrentActiveRetainedPayloadBytes);
        Assert.Throws<ArgumentNullException>(() =>
            explicitPressure.WithRetainedResourcePressure(null!));
    }

    [Fact]
    public void OwnedSnapshotAllocationSummaryComputesRatiosAndRejectsInvalidValues()
    {
        var summary = new RadarProcessingOwnedSnapshotAllocationSummary(
            snapshotCount: 2,
            payloadBytes: 128,
            payloadValueCount: 64,
            allocatedBytes: 256,
            elapsed: TimeSpan.FromMilliseconds(3));

        Assert.Equal(128.0, summary.AllocatedBytesPerSnapshot);
        Assert.Equal(2.0, summary.AllocatedBytesPerPayloadByte);
        Assert.Equal(4.0, summary.AllocatedBytesPerPayloadValue);
        Assert.Equal(64.0, summary.PayloadBytesPerSnapshot);
        Assert.Equal(0.0, RadarProcessingOwnedSnapshotAllocationSummary.Empty.AllocatedBytesPerPayloadValue);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingOwnedSnapshotAllocationSummary(snapshotCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingOwnedSnapshotAllocationSummary(elapsed: TimeSpan.FromTicks(-1)));
    }
}
