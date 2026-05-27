namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable provider queue telemetry snapshot.
/// </summary>
/// <remarks>
/// The summary aggregates owned-snapshot cost, enqueue/dequeue/processing counts,
/// retained payload pressure, recent diagnostic details, and latency totals for
/// validation and rollout gates.
/// </remarks>
public sealed record RadarProcessingProviderQueueTelemetrySummary
{
    /// <summary>
    /// Creates a telemetry summary with validated non-negative counters.
    /// </summary>
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

    /// <summary>
    /// Number of owned snapshots created for queued batches.
    /// </summary>
    public long OwnedSnapshotCount { get; }

    /// <summary>
    /// Payload bytes copied into owned queued snapshots.
    /// </summary>
    public long OwnedSnapshotPayloadBytes { get; }

    /// <summary>
    /// Allocated bytes attributed to owned snapshot creation.
    /// </summary>
    public long OwnedSnapshotAllocatedBytes { get; }

    /// <summary>
    /// Total time spent creating owned snapshots.
    /// </summary>
    public TimeSpan TotalOwnedSnapshotTime { get; }

    /// <summary>
    /// Stream events captured in owned snapshots.
    /// </summary>
    public long OwnedSnapshotEventCount { get; }

    /// <summary>
    /// Number of enqueue attempts made by the provider.
    /// </summary>
    public long EnqueueAttemptCount { get; }

    /// <summary>
    /// Number of batches accepted into the provider queue.
    /// </summary>
    public long EnqueuedBatchCount { get; }

    /// <summary>
    /// Number of enqueue attempts rejected because the queue was full.
    /// </summary>
    public long EnqueueFullCount { get; }

    /// <summary>
    /// Number of enqueue attempts that timed out.
    /// </summary>
    public long EnqueueTimedOutCount { get; }

    /// <summary>
    /// Number of enqueue attempts canceled before acceptance.
    /// </summary>
    public long EnqueueCanceledCount { get; }

    /// <summary>
    /// Number of enqueue attempts rejected after the queue closed.
    /// </summary>
    public long EnqueueClosedCount { get; }

    /// <summary>
    /// Number of enqueue attempts rejected after the queue faulted.
    /// </summary>
    public long EnqueueFaultedCount { get; }

    /// <summary>
    /// Total time spent waiting to enqueue provider batches.
    /// </summary>
    public TimeSpan TotalEnqueueWaitTime { get; }

    /// <summary>
    /// Total time spent waiting to dequeue provider batches.
    /// </summary>
    public TimeSpan TotalDequeueWaitTime { get; }

    /// <summary>
    /// Number of batches dequeued for processing.
    /// </summary>
    public long DequeuedBatchCount { get; }

    /// <summary>
    /// Number of dequeued batches completed successfully.
    /// </summary>
    public long CompletedBatchCount { get; }

    /// <summary>
    /// Number of dequeued batches failed by processing.
    /// </summary>
    public long FailedBatchCount { get; }

    /// <summary>
    /// Number of dequeued batches canceled before completion.
    /// </summary>
    public long CanceledBatchCount { get; }

    /// <summary>
    /// Number of dequeued batches skipped after a prior fault.
    /// </summary>
    public long SkippedAfterFaultCount { get; }

    /// <summary>
    /// Total time spent draining queued provider batches.
    /// </summary>
    public TimeSpan TotalDrainTime { get; }

    /// <summary>
    /// Maximum queue depth observed.
    /// </summary>
    public int QueueDepthHighWatermark { get; }

    /// <summary>
    /// Maximum queued payload bytes observed.
    /// </summary>
    public long QueuedPayloadBytesHighWatermark { get; }

    /// <summary>
    /// Maximum retained payload bytes observed by queue-only telemetry.
    /// </summary>
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

    /// <summary>
    /// Payload values copied into owned queued snapshots.
    /// </summary>
    public long OwnedSnapshotPayloadValueCount { get; }

    /// <summary>
    /// Total latency from provider acceptance to processing completion.
    /// </summary>
    public TimeSpan TotalProviderToProcessingLatency { get; }

    /// <summary>
    /// Retained recent queue details.
    /// </summary>
    public IReadOnlyList<RadarProcessingProviderQueueRecentDetail> RecentDetails { get; }

    /// <summary>
    /// Number of recent details retained.
    /// </summary>
    public int RetainedRecentDetailCount => RecentDetails.Count;

    /// <summary>
    /// Number of recent details dropped by retention limits.
    /// </summary>
    public long DroppedRecentDetailCount { get; }

    /// <summary>
    /// Allocation summary for owned queued snapshots.
    /// </summary>
    public RadarProcessingOwnedSnapshotAllocationSummary OwnedSnapshotAllocation { get; }

    /// <summary>
    /// Retained-resource pressure evidence for queued and active retained payloads.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSummary RetainedResourcePressure { get; }

    /// <summary>
    /// Gets whether queue telemetry observed backpressure.
    /// </summary>
    public bool HasBackpressure =>
        EnqueueFullCount > 0 ||
        EnqueueTimedOutCount > 0 ||
        TotalEnqueueWaitTime > TimeSpan.Zero;

    /// <summary>
    /// Returns a copy with externally supplied retained-resource pressure evidence.
    /// </summary>
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

    /// <summary>
    /// Empty telemetry summary.
    /// </summary>
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
