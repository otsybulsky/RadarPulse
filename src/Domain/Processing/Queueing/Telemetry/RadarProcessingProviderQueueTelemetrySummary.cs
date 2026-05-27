namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingProviderQueueTelemetrySummary
{
    public RadarProcessingProviderQueueTelemetrySummary(
        long ownedSnapshotCount = 0,
        long ownedSnapshotPayloadBytes = 0,
        long ownedSnapshotAllocatedBytes = 0,
        TimeSpan totalOwnedSnapshotTime = default,
        long enqueueAttemptCount = 0,
        long enqueuedBatchCount = 0,
        long enqueueFullCount = 0,
        long enqueueTimedOutCount = 0,
        long enqueueCanceledCount = 0,
        long enqueueClosedCount = 0,
        long enqueueFaultedCount = 0,
        TimeSpan totalEnqueueWaitTime = default,
        long dequeuedBatchCount = 0,
        long completedBatchCount = 0,
        long failedBatchCount = 0,
        long canceledBatchCount = 0,
        long skippedAfterFaultCount = 0,
        TimeSpan totalDrainTime = default,
        int queueDepthHighWatermark = 0,
        long queuedPayloadBytesHighWatermark = 0,
        long ownedSnapshotPayloadValueCount = 0,
        TimeSpan totalProviderToProcessingLatency = default,
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail>? recentDetails = null,
        long droppedRecentDetailCount = 0,
        long ownedSnapshotEventCount = 0,
        TimeSpan totalDequeueWaitTime = default,
        RadarProcessingRetainedResourcePressureSummary? retainedResourcePressure = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);
        EnsureNonNegative(totalOwnedSnapshotTime, nameof(totalOwnedSnapshotTime));
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueAttemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueuedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueFullCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueTimedOutCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueCanceledCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueClosedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueFaultedCount);
        EnsureNonNegative(totalEnqueueWaitTime, nameof(totalEnqueueWaitTime));
        EnsureNonNegative(totalDequeueWaitTime, nameof(totalDequeueWaitTime));
        ArgumentOutOfRangeException.ThrowIfNegative(dequeuedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedAfterFaultCount);
        EnsureNonNegative(totalDrainTime, nameof(totalDrainTime));
        ArgumentOutOfRangeException.ThrowIfNegative(queueDepthHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(queuedPayloadBytesHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotPayloadValueCount);
        EnsureNonNegative(totalProviderToProcessingLatency, nameof(totalProviderToProcessingLatency));
        ArgumentOutOfRangeException.ThrowIfNegative(droppedRecentDetailCount);

        if (enqueuedBatchCount > enqueueAttemptCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enqueuedBatchCount),
                enqueuedBatchCount,
                "Enqueued count cannot exceed enqueue attempt count.");
        }

        if (completedBatchCount + failedBatchCount + skippedAfterFaultCount > dequeuedBatchCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedBatchCount),
                completedBatchCount,
                "Completed, failed, and skipped-after-fault counts cannot exceed dequeued count.");
        }

        OwnedSnapshotCount = ownedSnapshotCount;
        OwnedSnapshotPayloadBytes = ownedSnapshotPayloadBytes;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
        TotalOwnedSnapshotTime = totalOwnedSnapshotTime;
        OwnedSnapshotEventCount = ownedSnapshotEventCount;
        EnqueueAttemptCount = enqueueAttemptCount;
        EnqueuedBatchCount = enqueuedBatchCount;
        EnqueueFullCount = enqueueFullCount;
        EnqueueTimedOutCount = enqueueTimedOutCount;
        EnqueueCanceledCount = enqueueCanceledCount;
        EnqueueClosedCount = enqueueClosedCount;
        EnqueueFaultedCount = enqueueFaultedCount;
        TotalEnqueueWaitTime = totalEnqueueWaitTime;
        TotalDequeueWaitTime = totalDequeueWaitTime;
        DequeuedBatchCount = dequeuedBatchCount;
        CompletedBatchCount = completedBatchCount;
        FailedBatchCount = failedBatchCount;
        CanceledBatchCount = canceledBatchCount;
        SkippedAfterFaultCount = skippedAfterFaultCount;
        TotalDrainTime = totalDrainTime;
        QueueDepthHighWatermark = queueDepthHighWatermark;
        QueuedPayloadBytesHighWatermark = queuedPayloadBytesHighWatermark;
        OwnedSnapshotPayloadValueCount = ownedSnapshotPayloadValueCount;
        TotalProviderToProcessingLatency = totalProviderToProcessingLatency;
        RecentDetails = CopyRequired(recentDetails ?? Array.Empty<RadarProcessingProviderQueueRecentDetail>(), nameof(recentDetails));
        DroppedRecentDetailCount = droppedRecentDetailCount;
        RetainedResourcePressure = retainedResourcePressure ??
                                   CreateQueueOnlyRetainedResourcePressure(
                                       queuedPayloadBytesHighWatermark,
                                       queueDepthHighWatermark);
        OwnedSnapshotAllocation = new RadarProcessingOwnedSnapshotAllocationSummary(
            OwnedSnapshotCount,
            OwnedSnapshotPayloadBytes,
            OwnedSnapshotPayloadValueCount,
            OwnedSnapshotAllocatedBytes,
            TotalOwnedSnapshotTime);
    }

    public long OwnedSnapshotCount { get; }

    public long OwnedSnapshotPayloadBytes { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public TimeSpan TotalOwnedSnapshotTime { get; }

    public long OwnedSnapshotEventCount { get; }

    public long EnqueueAttemptCount { get; }

    public long EnqueuedBatchCount { get; }

    public long EnqueueFullCount { get; }

    public long EnqueueTimedOutCount { get; }

    public long EnqueueCanceledCount { get; }

    public long EnqueueClosedCount { get; }

    public long EnqueueFaultedCount { get; }

    public TimeSpan TotalEnqueueWaitTime { get; }

    public TimeSpan TotalDequeueWaitTime { get; }

    public long DequeuedBatchCount { get; }

    public long CompletedBatchCount { get; }

    public long FailedBatchCount { get; }

    public long CanceledBatchCount { get; }

    public long SkippedAfterFaultCount { get; }

    public TimeSpan TotalDrainTime { get; }

    public int QueueDepthHighWatermark { get; }

    public long QueuedPayloadBytesHighWatermark { get; }

    public long RetainedPayloadBytesHighWatermark => QueuedPayloadBytesHighWatermark;

    public long CurrentPendingRetainedBatchCount =>
        RetainedResourcePressure.CurrentPendingRetainedBatchCount;

    public long CurrentPendingRetainedPayloadBytes =>
        RetainedResourcePressure.CurrentPendingRetainedPayloadBytes;

    public long PendingRetainedBatchCountHighWatermark =>
        RetainedResourcePressure.PendingRetainedBatchCountHighWatermark;

    public long PendingRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.PendingRetainedPayloadBytesHighWatermark;

    public long CurrentActiveRetainedBatchCount =>
        RetainedResourcePressure.CurrentActiveRetainedBatchCount;

    public long CurrentActiveRetainedPayloadBytes =>
        RetainedResourcePressure.CurrentActiveRetainedPayloadBytes;

    public long ActiveRetainedBatchCountHighWatermark =>
        RetainedResourcePressure.ActiveRetainedBatchCountHighWatermark;

    public long ActiveRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.ActiveRetainedPayloadBytesHighWatermark;

    public long CurrentCombinedRetainedBatchCount =>
        RetainedResourcePressure.CurrentCombinedRetainedBatchCount;

    public long CurrentCombinedRetainedPayloadBytes =>
        RetainedResourcePressure.CurrentCombinedRetainedPayloadBytes;

    public long CombinedRetainedBatchCountHighWatermark =>
        RetainedResourcePressure.CombinedRetainedBatchCountHighWatermark;

    public long CombinedRetainedPayloadBytesHighWatermark =>
        RetainedResourcePressure.CombinedRetainedPayloadBytesHighWatermark;

    public long OwnedSnapshotPayloadValueCount { get; }

    public TimeSpan TotalProviderToProcessingLatency { get; }

    public IReadOnlyList<RadarProcessingProviderQueueRecentDetail> RecentDetails { get; }

    public int RetainedRecentDetailCount => RecentDetails.Count;

    public long DroppedRecentDetailCount { get; }

    public RadarProcessingOwnedSnapshotAllocationSummary OwnedSnapshotAllocation { get; }

    public RadarProcessingRetainedResourcePressureSummary RetainedResourcePressure { get; }

    public bool HasBackpressure =>
        EnqueueFullCount > 0 ||
        EnqueueTimedOutCount > 0 ||
        TotalEnqueueWaitTime > TimeSpan.Zero;

    public RadarProcessingProviderQueueTelemetrySummary WithRetainedResourcePressure(
        RadarProcessingRetainedResourcePressureSummary retainedResourcePressure)
    {
        ArgumentNullException.ThrowIfNull(retainedResourcePressure);

        return new RadarProcessingProviderQueueTelemetrySummary(
            OwnedSnapshotCount,
            OwnedSnapshotPayloadBytes,
            OwnedSnapshotAllocatedBytes,
            TotalOwnedSnapshotTime,
            EnqueueAttemptCount,
            EnqueuedBatchCount,
            EnqueueFullCount,
            EnqueueTimedOutCount,
            EnqueueCanceledCount,
            EnqueueClosedCount,
            EnqueueFaultedCount,
            TotalEnqueueWaitTime,
            DequeuedBatchCount,
            CompletedBatchCount,
            FailedBatchCount,
            CanceledBatchCount,
            SkippedAfterFaultCount,
            TotalDrainTime,
            QueueDepthHighWatermark,
            QueuedPayloadBytesHighWatermark,
            OwnedSnapshotPayloadValueCount,
            TotalProviderToProcessingLatency,
            RecentDetails,
            DroppedRecentDetailCount,
            OwnedSnapshotEventCount,
            TotalDequeueWaitTime,
            retainedResourcePressure);
    }

    public static RadarProcessingProviderQueueTelemetrySummary Empty { get; } = new();

    private static void EnsureNonNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    private static IReadOnlyList<T> CopyRequired<T>(
        IReadOnlyCollection<T> values,
        string paramName)
        where T : class
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new T[values.Count];
        var index = 0;
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }

            result[index++] = value;
        }

        return Array.AsReadOnly(result);
    }

    private static RadarProcessingRetainedResourcePressureSummary CreateQueueOnlyRetainedResourcePressure(
        long queuedPayloadBytesHighWatermark,
        int queueDepthHighWatermark) =>
        new(
            pendingRetainedBatchCountHighWatermark: queueDepthHighWatermark,
            pendingRetainedPayloadBytesHighWatermark: queuedPayloadBytesHighWatermark,
            combinedRetainedBatchCountHighWatermark: queueDepthHighWatermark,
            combinedRetainedPayloadBytesHighWatermark: queuedPayloadBytesHighWatermark);
}
