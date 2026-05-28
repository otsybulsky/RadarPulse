using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderValidator
{
    private static RadarProcessingQueuedProviderValidationResult ValidateDiagnostic(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context)
    {
        var accepted = result.EnqueueResults.LongCount(static enqueue => enqueue.IsAccepted);
        var completed = result.ProcessingResults.LongCount(static processing => processing.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        var failed = result.ProcessingResults.LongCount(static processing => IsFailedProcessingStatus(processing.Status));
        var canceled = result.ProcessingResults.LongCount(static processing => processing.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = result.ProcessingResults.LongCount(static processing => processing.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);

        if (result.Telemetry.EnqueueAttemptCount != result.EnqueueResults.Count ||
            result.Telemetry.EnqueuedBatchCount != accepted ||
            result.Telemetry.CompletedBatchCount != completed ||
            result.Telemetry.FailedBatchCount != failed ||
            result.Telemetry.CanceledBatchCount != canceled ||
            result.Telemetry.SkippedAfterFaultCount != skipped ||
            result.Telemetry.DequeuedBatchCount < result.ProcessingResults.Count)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.TelemetryCounterMismatch,
                "Queued provider telemetry counters must match enqueue and processing result snapshots.",
                profile,
                context: context);
        }

        var queueTelemetry = ValidateQueueTelemetrySnapshot(result, accepted, profile, context);
        if (!queueTelemetry.IsValid)
        {
            return queueTelemetry;
        }

        if (result.IsCompleted && failed > 0)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.QueueFaultStateMismatch,
                "Completed queued provider sessions must not contain failed processing results.",
                profile,
                context: context);
        }

        var metrics = CreateMetrics(result);
        if (metrics.WorkerFailedBatchCount > 0 && failed == 0 && canceled == 0)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.WorkerFailureCountMismatch,
                "Worker failure telemetry must be reflected by failed or canceled queued batch results.",
                profile,
                context: context);
        }

        var optimizedTelemetry = ValidateOptimizedTelemetry(result, context, profile);
        if (!optimizedTelemetry.IsValid)
        {
            return optimizedTelemetry;
        }

        return Valid(profile, context);
    }

}
