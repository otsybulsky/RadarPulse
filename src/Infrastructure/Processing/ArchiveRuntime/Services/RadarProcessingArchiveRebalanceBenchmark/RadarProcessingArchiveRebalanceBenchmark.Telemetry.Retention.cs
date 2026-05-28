using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
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
}
