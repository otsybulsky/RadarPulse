namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingProviderQueueTelemetryRecorder
{
    private readonly object sync = new();
    private readonly RadarProcessingProviderQueueOptions options;
    private readonly RadarProcessingBoundedTelemetryWindow<RadarProcessingProviderQueueRecentDetail> recentDetails;
    private long ownedSnapshotCount;
    private long ownedSnapshotEventCount;
    private long ownedSnapshotPayloadBytes;
    private long ownedSnapshotPayloadValueCount;
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
    private long completedBatchCount;
    private long failedBatchCount;
    private long canceledBatchCount;
    private long skippedAfterFaultCount;
    private TimeSpan totalDrainTime;
    private TimeSpan totalProviderToProcessingLatency;
    private int queueDepthHighWatermark;
    private long queuedPayloadBytesHighWatermark;

    public RadarProcessingProviderQueueTelemetryRecorder(
        RadarProcessingProviderQueueOptions? options = null)
    {
        this.options = options ?? RadarProcessingProviderQueueOptions.Default;
        recentDetails = new RadarProcessingBoundedTelemetryWindow<RadarProcessingProviderQueueRecentDetail>(
            this.options.RecentDetailCapacity);
    }

    public RadarProcessingProviderQueueOptions Options => options;

    public void RecordEnqueueResult(
        RadarProcessingQueuedBatchEnqueueResult result,
        int queueDepth = 0,
        long queuedPayloadBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(queueDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(queuedPayloadBytes);

        lock (sync)
        {
            enqueueAttemptCount++;
            totalEnqueueWaitTime += result.EnqueueWaitTime;

            switch (result.Status)
            {
                case RadarProcessingQueuedBatchEnqueueStatus.Accepted:
                    var batch = result.Batch ??
                                throw new ArgumentException("Accepted enqueue results require a queued batch.", nameof(result));
                    enqueuedBatchCount++;
                    ownedSnapshotCount++;
                    ownedSnapshotEventCount = checked(ownedSnapshotEventCount + batch.StreamEventCount);
                    ownedSnapshotPayloadBytes = checked(ownedSnapshotPayloadBytes + batch.PayloadBytes);
                    ownedSnapshotPayloadValueCount = checked(ownedSnapshotPayloadValueCount + batch.PayloadValueCount);
                    ownedSnapshotAllocatedBytes = checked(ownedSnapshotAllocatedBytes + batch.OwnedSnapshotAllocatedBytes);
                    totalOwnedSnapshotTime += batch.OwnedSnapshotTime;
                    queueDepthHighWatermark = Math.Max(queueDepthHighWatermark, queueDepth);
                    queuedPayloadBytesHighWatermark = Math.Max(queuedPayloadBytesHighWatermark, queuedPayloadBytes);
                    break;

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
                    RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(result.Status);
                    throw new ArgumentOutOfRangeException(nameof(result));
            }

            AddRecentDetailUnsafe(
                RadarProcessingProviderQueueRecentDetail.FromEnqueueResult(
                    result,
                    queueDepth,
                    queuedPayloadBytes));
        }
    }

    public void RecordDequeuedBatch(
        RadarProcessingQueuedBatch batch,
        TimeSpan providerToProcessingLatency = default,
        int queueDepth = 0,
        long queuedPayloadBytes = 0,
        TimeSpan dequeueWaitTime = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        EnsureNonNegative(providerToProcessingLatency, nameof(providerToProcessingLatency));
        ArgumentOutOfRangeException.ThrowIfNegative(queueDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(queuedPayloadBytes);
        EnsureNonNegative(dequeueWaitTime, nameof(dequeueWaitTime));

        lock (sync)
        {
            dequeuedBatchCount++;
            totalDequeueWaitTime += dequeueWaitTime;
            totalProviderToProcessingLatency += providerToProcessingLatency;
            queueDepthHighWatermark = Math.Max(queueDepthHighWatermark, queueDepth);
            queuedPayloadBytesHighWatermark = Math.Max(queuedPayloadBytesHighWatermark, queuedPayloadBytes);
            AddRecentDetailUnsafe(
                RadarProcessingProviderQueueRecentDetail.FromDequeuedBatch(
                    batch,
                    providerToProcessingLatency,
                    queueDepth,
                    queuedPayloadBytes));
        }
    }

    public void RecordProcessingResult(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (sync)
        {
            switch (result.Status)
            {
                case RadarProcessingQueuedBatchProcessingStatus.Succeeded:
                    completedBatchCount++;
                    break;

                case RadarProcessingQueuedBatchProcessingStatus.FailedProcessing:
                case RadarProcessingQueuedBatchProcessingStatus.FailedValidation:
                case RadarProcessingQueuedBatchProcessingStatus.FailedMigration:
                    failedBatchCount++;
                    break;

                case RadarProcessingQueuedBatchProcessingStatus.Canceled:
                    canceledBatchCount++;
                    break;

                case RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault:
                    skippedAfterFaultCount++;
                    break;

                default:
                    RadarProcessingQueuedBatchProcessingResult.EnsureKnownStatus(result.Status);
                    throw new ArgumentOutOfRangeException(nameof(result));
            }

            AddRecentDetailUnsafe(RadarProcessingProviderQueueRecentDetail.FromProcessingResult(result));
        }
    }

    public void AddDrainTime(TimeSpan drainTime)
    {
        EnsureNonNegative(drainTime, nameof(drainTime));
        lock (sync)
        {
            totalDrainTime += drainTime;
        }
    }

    public RadarProcessingProviderQueueTelemetrySummary CreateSummary()
    {
        lock (sync)
        {
            return new RadarProcessingProviderQueueTelemetrySummary(
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
                completedBatchCount,
                failedBatchCount,
                canceledBatchCount,
                skippedAfterFaultCount,
                totalDrainTime,
                queueDepthHighWatermark,
                queuedPayloadBytesHighWatermark,
                ownedSnapshotPayloadValueCount,
                totalProviderToProcessingLatency,
                recentDetails.Snapshot(),
                recentDetails.DroppedCount,
                ownedSnapshotEventCount,
                totalDequeueWaitTime);
        }
    }

    public void Reset()
    {
        lock (sync)
        {
            ownedSnapshotCount = 0;
            ownedSnapshotEventCount = 0;
            ownedSnapshotPayloadBytes = 0;
            ownedSnapshotPayloadValueCount = 0;
            ownedSnapshotAllocatedBytes = 0;
            totalOwnedSnapshotTime = TimeSpan.Zero;
            enqueueAttemptCount = 0;
            enqueuedBatchCount = 0;
            enqueueFullCount = 0;
            enqueueTimedOutCount = 0;
            enqueueCanceledCount = 0;
            enqueueClosedCount = 0;
            enqueueFaultedCount = 0;
            totalEnqueueWaitTime = TimeSpan.Zero;
            totalDequeueWaitTime = TimeSpan.Zero;
            dequeuedBatchCount = 0;
            completedBatchCount = 0;
            failedBatchCount = 0;
            canceledBatchCount = 0;
            skippedAfterFaultCount = 0;
            totalDrainTime = TimeSpan.Zero;
            totalProviderToProcessingLatency = TimeSpan.Zero;
            queueDepthHighWatermark = 0;
            queuedPayloadBytesHighWatermark = 0;
            recentDetails.Clear();
        }
    }

    private void AddRecentDetailUnsafe(
        RadarProcessingProviderQueueRecentDetail detail)
    {
        if (!recentDetails.CanRetain)
        {
            recentDetails.Drop();
            return;
        }

        recentDetails.Add(detail);
    }

    private static void EnsureNonNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
