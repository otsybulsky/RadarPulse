using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static RadarProcessingProviderQueueTelemetrySummary WithQueueCompletionTelemetry(
        RadarProcessingProviderQueueTelemetrySummary queueTelemetry,
        long completed,
        long failed,
        long canceled,
        long skippedAfterFault,
        TimeSpan drainTime)
    {
        ArgumentNullException.ThrowIfNull(queueTelemetry);
        ArgumentOutOfRangeException.ThrowIfNegative(completed);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(canceled);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedAfterFault);
        if (drainTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(drainTime));
        }

        return new RadarProcessingProviderQueueTelemetrySummary(
            queueTelemetry.OwnedSnapshotCount,
            queueTelemetry.OwnedSnapshotPayloadBytes,
            queueTelemetry.OwnedSnapshotAllocatedBytes,
            queueTelemetry.TotalOwnedSnapshotTime,
            queueTelemetry.EnqueueAttemptCount,
            queueTelemetry.EnqueuedBatchCount,
            queueTelemetry.EnqueueFullCount,
            queueTelemetry.EnqueueTimedOutCount,
            queueTelemetry.EnqueueCanceledCount,
            queueTelemetry.EnqueueClosedCount,
            queueTelemetry.EnqueueFaultedCount,
            queueTelemetry.TotalEnqueueWaitTime,
            queueTelemetry.DequeuedBatchCount,
            completed,
            failed,
            canceled,
            skippedAfterFault,
            queueTelemetry.TotalDrainTime + drainTime,
            queueTelemetry.QueueDepthHighWatermark,
            queueTelemetry.QueuedPayloadBytesHighWatermark,
            queueTelemetry.OwnedSnapshotPayloadValueCount,
            queueTelemetry.TotalProviderToProcessingLatency,
            queueTelemetry.RecentDetails,
            queueTelemetry.DroppedRecentDetailCount,
            queueTelemetry.OwnedSnapshotEventCount,
            queueTelemetry.TotalDequeueWaitTime,
            queueTelemetry.RetainedResourcePressure);
    }

    private static RadarProcessingProviderQueueTelemetrySummary AddQueueTelemetry(
        RadarProcessingProviderQueueTelemetrySummary current,
        RadarProcessingProviderQueueTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var recentDetails = CreateBoundedRecentDetails(
            current.RecentDetails,
            next.RecentDetails,
            out var droppedRecentDetails);

        return new RadarProcessingProviderQueueTelemetrySummary(
            checked(current.OwnedSnapshotCount + next.OwnedSnapshotCount),
            checked(current.OwnedSnapshotPayloadBytes + next.OwnedSnapshotPayloadBytes),
            checked(current.OwnedSnapshotAllocatedBytes + next.OwnedSnapshotAllocatedBytes),
            current.TotalOwnedSnapshotTime + next.TotalOwnedSnapshotTime,
            checked(current.EnqueueAttemptCount + next.EnqueueAttemptCount),
            checked(current.EnqueuedBatchCount + next.EnqueuedBatchCount),
            checked(current.EnqueueFullCount + next.EnqueueFullCount),
            checked(current.EnqueueTimedOutCount + next.EnqueueTimedOutCount),
            checked(current.EnqueueCanceledCount + next.EnqueueCanceledCount),
            checked(current.EnqueueClosedCount + next.EnqueueClosedCount),
            checked(current.EnqueueFaultedCount + next.EnqueueFaultedCount),
            current.TotalEnqueueWaitTime + next.TotalEnqueueWaitTime,
            checked(current.DequeuedBatchCount + next.DequeuedBatchCount),
            checked(current.CompletedBatchCount + next.CompletedBatchCount),
            checked(current.FailedBatchCount + next.FailedBatchCount),
            checked(current.CanceledBatchCount + next.CanceledBatchCount),
            checked(current.SkippedAfterFaultCount + next.SkippedAfterFaultCount),
            current.TotalDrainTime + next.TotalDrainTime,
            Math.Max(current.QueueDepthHighWatermark, next.QueueDepthHighWatermark),
            Math.Max(current.QueuedPayloadBytesHighWatermark, next.QueuedPayloadBytesHighWatermark),
            checked(current.OwnedSnapshotPayloadValueCount + next.OwnedSnapshotPayloadValueCount),
            current.TotalProviderToProcessingLatency + next.TotalProviderToProcessingLatency,
            recentDetails,
            checked(current.DroppedRecentDetailCount + next.DroppedRecentDetailCount + droppedRecentDetails),
            checked(current.OwnedSnapshotEventCount + next.OwnedSnapshotEventCount),
            current.TotalDequeueWaitTime + next.TotalDequeueWaitTime,
            AddRetainedResourcePressure(current.RetainedResourcePressure, next.RetainedResourcePressure));
    }

    private static IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> CreateBoundedRecentDetails(
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> current,
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> next,
        out long droppedRecentDetails)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var capacity = RadarProcessingProviderQueueOptions.Default.RecentDetailCapacity;
        var totalCount = checked(current.Count + next.Count);
        var skipCount = Math.Max(0, totalCount - capacity);
        droppedRecentDetails = skipCount;
        if (capacity == 0 || totalCount == 0)
        {
            return Array.Empty<RadarProcessingProviderQueueRecentDetail>();
        }

        var retainedCount = totalCount - skipCount;
        var result = new RadarProcessingProviderQueueRecentDetail[retainedCount];
        var writeIndex = 0;
        var readIndex = 0;

        CopyRecentDetails(current, skipCount, result, ref readIndex, ref writeIndex);
        CopyRecentDetails(next, skipCount, result, ref readIndex, ref writeIndex);

        return result;
    }

    private static void CopyRecentDetails(
        IReadOnlyCollection<RadarProcessingProviderQueueRecentDetail> source,
        int skipCount,
        RadarProcessingProviderQueueRecentDetail[] destination,
        ref int readIndex,
        ref int writeIndex)
    {
        foreach (var detail in source)
        {
            if (readIndex++ < skipCount)
            {
                continue;
            }

            destination[writeIndex++] = detail;
        }
    }
}
