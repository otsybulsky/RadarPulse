using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderValidator
{
    private static RadarProcessingQueuedProviderValidationResult ValidateQueueTelemetrySnapshot(
        RadarProcessingQueuedSessionResult result,
        long accepted,
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context)
    {
        var acceptedEventCount = 0L;
        var acceptedPayloadBytes = 0L;
        var acceptedPayloadValueCount = 0L;
        foreach (var enqueue in result.EnqueueResults)
        {
            if (!enqueue.IsAccepted)
            {
                continue;
            }

            var batch = enqueue.Batch ??
                        throw new InvalidOperationException("Accepted enqueue results must carry a queued batch.");
            acceptedEventCount = checked(acceptedEventCount + batch.StreamEventCount);
            acceptedPayloadBytes = checked(acceptedPayloadBytes + batch.PayloadBytes);
            acceptedPayloadValueCount = checked(acceptedPayloadValueCount + batch.PayloadValueCount);
        }

        if (result.Telemetry.OwnedSnapshotCount != accepted ||
            result.Telemetry.OwnedSnapshotEventCount != acceptedEventCount ||
            result.Telemetry.OwnedSnapshotPayloadBytes != acceptedPayloadBytes ||
            result.Telemetry.OwnedSnapshotPayloadValueCount != acceptedPayloadValueCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.TelemetryCounterMismatch,
                "Queued provider retained payload telemetry must match accepted queued batches.",
                profile,
                context: context);
        }

        return Valid(profile, context);
    }

    private static RadarProcessingQueuedProviderValidationResult ValidateOptimizedTelemetry(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationContext? context,
        RadarProcessingQueuedProviderValidationProfile profile)
    {
        if (context is null)
        {
            return Valid(profile, context);
        }

        if (context.RequiresOverlapTelemetry && context.OverlapElapsed == TimeSpan.Zero && result.IsCompleted)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.OverlapTelemetryIncomplete,
                "Producer-consumer overlap validation requires positive overlap telemetry for completed sessions.",
                profile,
                context: context);
        }

        var retention = context.RetentionTelemetry;
        if (result.Telemetry.EnqueuedBatchCount > 0 && !context.HasRetentionTelemetry)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.RetentionTelemetryIncomplete,
                "Queued provider validation requires retention telemetry for accepted retained batches.",
                profile,
                context: context);
        }

        if (context.HasRetentionTelemetry &&
            (retention.RetainedBatchCount != result.Telemetry.OwnedSnapshotCount ||
             retention.RetainedEventCount != result.Telemetry.OwnedSnapshotEventCount ||
             retention.RetainedPayloadBytes != result.Telemetry.OwnedSnapshotPayloadBytes ||
             retention.RetainedPayloadValueCount != result.Telemetry.OwnedSnapshotPayloadValueCount ||
             retention.AllocatedBytes != result.Telemetry.OwnedSnapshotAllocatedBytes))
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.RetentionTelemetryMismatch,
                "Retention telemetry must match queued owned snapshot counters.",
                profile,
                context: context);
        }

        if (!context.HasRetentionTelemetry)
        {
            return Valid(profile, context);
        }

        var completedReleaseCount = checked(
            retention.ReleasedBatchCount +
            retention.AlreadyReleasedBatchCount +
            retention.ReleaseNotRequiredCount);
        if (retention.ReleaseFailedCount > 0 ||
            completedReleaseCount < retention.RetainedBatchCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.RetainedResourceCleanupIncomplete,
                "Retained resources must be released or explicitly marked as release-not-required at session completion.",
                profile,
                context: context,
                expectedCount: retention.RetainedBatchCount,
                actualCount: completedReleaseCount);
        }

        return Valid(profile, context);
    }

}
