using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

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

    private static RadarProcessingRetainedResourcePressureSummary AddRetainedResourcePressure(
        RadarProcessingRetainedResourcePressureSummary current,
        RadarProcessingRetainedResourcePressureSummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var currentPendingBatchCount = checked(
            current.CurrentPendingRetainedBatchCount +
            next.CurrentPendingRetainedBatchCount);
        var currentPendingPayloadBytes = checked(
            current.CurrentPendingRetainedPayloadBytes +
            next.CurrentPendingRetainedPayloadBytes);
        var currentActiveBatchCount = checked(
            current.CurrentActiveRetainedBatchCount +
            next.CurrentActiveRetainedBatchCount);
        var currentActivePayloadBytes = checked(
            current.CurrentActiveRetainedPayloadBytes +
            next.CurrentActiveRetainedPayloadBytes);
        var pendingBatchHighWatermark = Math.Max(
            Math.Max(current.PendingRetainedBatchCountHighWatermark, next.PendingRetainedBatchCountHighWatermark),
            currentPendingBatchCount);
        var pendingPayloadHighWatermark = Math.Max(
            Math.Max(current.PendingRetainedPayloadBytesHighWatermark, next.PendingRetainedPayloadBytesHighWatermark),
            currentPendingPayloadBytes);
        var activeBatchHighWatermark = Math.Max(
            Math.Max(current.ActiveRetainedBatchCountHighWatermark, next.ActiveRetainedBatchCountHighWatermark),
            currentActiveBatchCount);
        var activePayloadHighWatermark = Math.Max(
            Math.Max(current.ActiveRetainedPayloadBytesHighWatermark, next.ActiveRetainedPayloadBytesHighWatermark),
            currentActivePayloadBytes);
        var currentCombinedBatchCount = checked(currentPendingBatchCount + currentActiveBatchCount);
        var currentCombinedPayloadBytes = checked(currentPendingPayloadBytes + currentActivePayloadBytes);

        return new RadarProcessingRetainedResourcePressureSummary(
            currentPendingBatchCount,
            currentPendingPayloadBytes,
            pendingBatchHighWatermark,
            pendingPayloadHighWatermark,
            currentActiveBatchCount,
            currentActivePayloadBytes,
            activeBatchHighWatermark,
            activePayloadHighWatermark,
            Math.Max(
                Math.Max(
                    current.CombinedRetainedBatchCountHighWatermark,
                    next.CombinedRetainedBatchCountHighWatermark),
                currentCombinedBatchCount),
            Math.Max(
                Math.Max(
                    current.CombinedRetainedPayloadBytesHighWatermark,
                    next.CombinedRetainedPayloadBytesHighWatermark),
                currentCombinedPayloadBytes));
    }

    private static RadarProcessingRetainedPayloadTelemetrySummary AddRetentionTelemetry(
        RadarProcessingRetainedPayloadTelemetrySummary current,
        RadarProcessingRetainedPayloadTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var strategy = next.RetentionAttemptCount > 0 || current.RetentionAttemptCount == 0
            ? next.Strategy
            : current.Strategy;
        if (current.RetentionAttemptCount > 0 &&
            next.RetentionAttemptCount > 0 &&
            current.Strategy != next.Strategy)
        {
            throw new InvalidOperationException("Cannot aggregate retained payload telemetry from different strategies.");
        }

        return new RadarProcessingRetainedPayloadTelemetrySummary(
            strategy,
            checked(current.RetentionAttemptCount + next.RetentionAttemptCount),
            checked(current.RetainedBatchCount + next.RetainedBatchCount),
            checked(current.RetentionUnsupportedStrategyCount + next.RetentionUnsupportedStrategyCount),
            checked(current.RetentionFailedCopyCount + next.RetentionFailedCopyCount),
            checked(current.RetentionCanceledCount + next.RetentionCanceledCount),
            checked(current.RetentionInvalidInputCount + next.RetentionInvalidInputCount),
            checked(current.RetainedEventCount + next.RetainedEventCount),
            checked(current.RetainedPayloadBytes + next.RetainedPayloadBytes),
            checked(current.RetainedPayloadValueCount + next.RetainedPayloadValueCount),
            checked(current.AllocatedBytes + next.AllocatedBytes),
            current.TotalRetentionTime + next.TotalRetentionTime,
            checked(current.TransferCount + next.TransferCount),
            checked(current.PoolRentCount + next.PoolRentCount),
            checked(current.PoolReturnCount + next.PoolReturnCount),
            checked(current.PoolMissCount + next.PoolMissCount),
            checked(current.ReleaseAttemptCount + next.ReleaseAttemptCount),
            checked(current.ReleasedBatchCount + next.ReleasedBatchCount),
            checked(current.AlreadyReleasedBatchCount + next.AlreadyReleasedBatchCount),
            checked(current.ReleaseFailedCount + next.ReleaseFailedCount),
            checked(current.ReleaseNotRequiredCount + next.ReleaseNotRequiredCount),
            current.TotalReleaseTime + next.TotalReleaseTime,
            eventPoolRentCount: checked(current.EventPoolRentCount + next.EventPoolRentCount),
            eventPoolReturnCount: checked(current.EventPoolReturnCount + next.EventPoolReturnCount),
            eventPoolMissCount: checked(current.EventPoolMissCount + next.EventPoolMissCount),
            payloadPoolRentCount: checked(current.PayloadPoolRentCount + next.PayloadPoolRentCount),
            payloadPoolReturnCount: checked(current.PayloadPoolReturnCount + next.PayloadPoolReturnCount),
            payloadPoolMissCount: checked(current.PayloadPoolMissCount + next.PayloadPoolMissCount));
    }

    private static RadarProcessingArchiveOverlapTelemetrySummary AddOverlapTelemetry(
        RadarProcessingArchiveOverlapTelemetrySummary current,
        RadarProcessingArchiveOverlapTelemetrySummary next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        var hasCurrent = current.Elapsed > TimeSpan.Zero ||
            current.ProducerActiveTime > TimeSpan.Zero ||
            current.ConsumerActiveTime > TimeSpan.Zero ||
            current.OverlapElapsed > TimeSpan.Zero;
        var hasNext = next.Elapsed > TimeSpan.Zero ||
            next.ProducerActiveTime > TimeSpan.Zero ||
            next.ConsumerActiveTime > TimeSpan.Zero ||
            next.OverlapElapsed > TimeSpan.Zero;
        var strategy = hasNext || !hasCurrent
            ? next.RetentionStrategy
            : current.RetentionStrategy;
        if (hasCurrent &&
            hasNext &&
            current.RetentionStrategy != next.RetentionStrategy)
        {
            throw new InvalidOperationException("Cannot aggregate overlap telemetry from different retention strategies.");
        }

        return new RadarProcessingArchiveOverlapTelemetrySummary(
            strategy,
            current.Elapsed + next.Elapsed,
            current.ProducerActiveTime + next.ProducerActiveTime,
            current.ConsumerActiveTime + next.ConsumerActiveTime,
            current.OverlapElapsed + next.OverlapElapsed,
            checked(current.MeasuredAllocatedBytes + next.MeasuredAllocatedBytes),
            AddQueueTelemetry(current.QueueTelemetry, next.QueueTelemetry),
            AddRetentionTelemetry(current.RetentionTelemetry, next.RetentionTelemetry));
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


    private static IReadOnlyList<T> CreateReadOnlyList<T>(List<T>? values) =>
        values is { Count: > 0 }
            ? Array.AsReadOnly(values.ToArray())
            : Array.Empty<T>();

    private static IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> CreateSortedSkippedReasonCounters(
        List<RadarProcessingRebalanceSkippedReasonCounter>? values)
    {
        if (values is not { Count: > 0 })
        {
            return Array.Empty<RadarProcessingRebalanceSkippedReasonCounter>();
        }

        var result = values.ToArray();
        Array.Sort(result, (left, right) => left.Reason.CompareTo(right.Reason));
        return Array.AsReadOnly(result);
    }
}
