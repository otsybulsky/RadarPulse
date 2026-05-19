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
        long queuedPayloadBytesHighWatermark = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);
        EnsureNonNegative(totalOwnedSnapshotTime, nameof(totalOwnedSnapshotTime));
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueAttemptCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueuedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueFullCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueTimedOutCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueCanceledCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueClosedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueueFaultedCount);
        EnsureNonNegative(totalEnqueueWaitTime, nameof(totalEnqueueWaitTime));
        ArgumentOutOfRangeException.ThrowIfNegative(dequeuedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(completedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(canceledBatchCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedAfterFaultCount);
        EnsureNonNegative(totalDrainTime, nameof(totalDrainTime));
        ArgumentOutOfRangeException.ThrowIfNegative(queueDepthHighWatermark);
        ArgumentOutOfRangeException.ThrowIfNegative(queuedPayloadBytesHighWatermark);

        if (enqueuedBatchCount > enqueueAttemptCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enqueuedBatchCount),
                enqueuedBatchCount,
                "Enqueued count cannot exceed enqueue attempt count.");
        }

        if (completedBatchCount + failedBatchCount + canceledBatchCount > dequeuedBatchCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedBatchCount),
                completedBatchCount,
                "Completed, failed, and canceled counts cannot exceed dequeued count.");
        }

        OwnedSnapshotCount = ownedSnapshotCount;
        OwnedSnapshotPayloadBytes = ownedSnapshotPayloadBytes;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
        TotalOwnedSnapshotTime = totalOwnedSnapshotTime;
        EnqueueAttemptCount = enqueueAttemptCount;
        EnqueuedBatchCount = enqueuedBatchCount;
        EnqueueFullCount = enqueueFullCount;
        EnqueueTimedOutCount = enqueueTimedOutCount;
        EnqueueCanceledCount = enqueueCanceledCount;
        EnqueueClosedCount = enqueueClosedCount;
        EnqueueFaultedCount = enqueueFaultedCount;
        TotalEnqueueWaitTime = totalEnqueueWaitTime;
        DequeuedBatchCount = dequeuedBatchCount;
        CompletedBatchCount = completedBatchCount;
        FailedBatchCount = failedBatchCount;
        CanceledBatchCount = canceledBatchCount;
        SkippedAfterFaultCount = skippedAfterFaultCount;
        TotalDrainTime = totalDrainTime;
        QueueDepthHighWatermark = queueDepthHighWatermark;
        QueuedPayloadBytesHighWatermark = queuedPayloadBytesHighWatermark;
    }

    public long OwnedSnapshotCount { get; }

    public long OwnedSnapshotPayloadBytes { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public TimeSpan TotalOwnedSnapshotTime { get; }

    public long EnqueueAttemptCount { get; }

    public long EnqueuedBatchCount { get; }

    public long EnqueueFullCount { get; }

    public long EnqueueTimedOutCount { get; }

    public long EnqueueCanceledCount { get; }

    public long EnqueueClosedCount { get; }

    public long EnqueueFaultedCount { get; }

    public TimeSpan TotalEnqueueWaitTime { get; }

    public long DequeuedBatchCount { get; }

    public long CompletedBatchCount { get; }

    public long FailedBatchCount { get; }

    public long CanceledBatchCount { get; }

    public long SkippedAfterFaultCount { get; }

    public TimeSpan TotalDrainTime { get; }

    public int QueueDepthHighWatermark { get; }

    public long QueuedPayloadBytesHighWatermark { get; }

    public bool HasBackpressure =>
        EnqueueFullCount > 0 ||
        EnqueueTimedOutCount > 0 ||
        TotalEnqueueWaitTime > TimeSpan.Zero;

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
}
