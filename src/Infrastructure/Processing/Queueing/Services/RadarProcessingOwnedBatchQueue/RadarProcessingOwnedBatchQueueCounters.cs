using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

internal sealed class RadarProcessingOwnedBatchQueueCounters
{
    private long ownedSnapshotCount;
    private long ownedSnapshotEventCount;
    private long ownedSnapshotPayloadBytes;
    private long ownedSnapshotAllocatedBytes;
    private TimeSpan totalOwnedSnapshotTime;
    private long enqueueAttemptCount;
    private long enqueuedBatchCount;
    private long enqueueFullCount;
    private long enqueueTimedOutCount;
    private long enqueueCanceledCount;
    private long enqueueClosedCount;
    private long enqueueFaultedCount;
    private TimeSpan totalEnqueueWaitTime;
    private TimeSpan totalDequeueWaitTime;
    private long dequeuedBatchCount;
    private int queueDepthHighWatermark;
    private long queuedPayloadBytesHighWatermark;

    public void RecordAccepted(
        RadarProcessingQueuedBatch batch,
        TimeSpan enqueueWaitTime,
        int pendingCount,
        long pendingPayloadBytes)
    {
        ownedSnapshotCount++;
        ownedSnapshotEventCount = checked(ownedSnapshotEventCount + batch.StreamEventCount);
        ownedSnapshotPayloadBytes = checked(ownedSnapshotPayloadBytes + batch.PayloadBytes);
        ownedSnapshotAllocatedBytes = checked(ownedSnapshotAllocatedBytes + batch.OwnedSnapshotAllocatedBytes);
        totalOwnedSnapshotTime += batch.OwnedSnapshotTime;
        enqueueAttemptCount++;
        enqueuedBatchCount++;
        totalEnqueueWaitTime += enqueueWaitTime;
        queueDepthHighWatermark = Math.Max(queueDepthHighWatermark, pendingCount);
        queuedPayloadBytesHighWatermark = Math.Max(queuedPayloadBytesHighWatermark, pendingPayloadBytes);
    }

    public void RecordRejected(
        RadarProcessingQueuedBatchEnqueueStatus status,
        TimeSpan enqueueWaitTime)
    {
        enqueueAttemptCount++;
        totalEnqueueWaitTime += enqueueWaitTime;
        switch (status)
        {
            case RadarProcessingQueuedBatchEnqueueStatus.Full:
                enqueueFullCount++;
                break;

            case RadarProcessingQueuedBatchEnqueueStatus.TimedOut:
                enqueueTimedOutCount++;
                break;

            case RadarProcessingQueuedBatchEnqueueStatus.Canceled:
                enqueueCanceledCount++;
                break;

            case RadarProcessingQueuedBatchEnqueueStatus.Closed:
                enqueueClosedCount++;
                break;

            case RadarProcessingQueuedBatchEnqueueStatus.Faulted:
                enqueueFaultedCount++;
                break;

            default:
                RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(status);
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    public void RecordDequeued(TimeSpan dequeueWaitTime)
    {
        dequeuedBatchCount++;
        totalDequeueWaitTime += dequeueWaitTime;
    }

    public void AddDequeueWaitTime(TimeSpan dequeueWaitTime)
    {
        totalDequeueWaitTime += dequeueWaitTime;
    }

    public RadarProcessingProviderQueueTelemetrySummary CreateSummary(
        RadarProcessingProviderQueueTelemetrySummary recordedSummary,
        int pendingCount,
        long pendingPayloadBytes) =>
        new(
            ownedSnapshotCount,
            ownedSnapshotPayloadBytes,
            ownedSnapshotAllocatedBytes,
            totalOwnedSnapshotTime,
            enqueueAttemptCount,
            enqueuedBatchCount,
            enqueueFullCount,
            enqueueTimedOutCount,
            enqueueCanceledCount,
            enqueueClosedCount,
            enqueueFaultedCount,
            totalEnqueueWaitTime,
            dequeuedBatchCount,
            completedBatchCount: 0,
            failedBatchCount: 0,
            canceledBatchCount: 0,
            skippedAfterFaultCount: 0,
            totalDrainTime: TimeSpan.Zero,
            queueDepthHighWatermark,
            queuedPayloadBytesHighWatermark,
            recordedSummary.OwnedSnapshotPayloadValueCount,
            recordedSummary.TotalProviderToProcessingLatency,
            recordedSummary.RecentDetails,
            recordedSummary.DroppedRecentDetailCount,
            ownedSnapshotEventCount,
            totalDequeueWaitTime,
            new RadarProcessingRetainedResourcePressureSummary(
                currentPendingRetainedBatchCount: pendingCount,
                currentPendingRetainedPayloadBytes: pendingPayloadBytes,
                pendingRetainedBatchCountHighWatermark: queueDepthHighWatermark,
                pendingRetainedPayloadBytesHighWatermark: queuedPayloadBytesHighWatermark,
                combinedRetainedBatchCountHighWatermark: queueDepthHighWatermark,
                combinedRetainedPayloadBytesHighWatermark: queuedPayloadBytesHighWatermark));
}
