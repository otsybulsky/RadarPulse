namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingAsyncValidator
{
    private static RadarProcessingAsyncValidationResult ValidateProcessingResultEssential(
        RadarProcessingResult result,
        RadarProcessingValidationProfile validationProfile)
    {
        if (result.IsValid)
        {
            if (result.Telemetry is null)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.MissingProcessingTelemetry,
                    "Valid async processing results must carry processing telemetry.",
                    validationProfile);
            }

            if (result.WorkerTelemetry is null ||
                result.WorkerTelemetry.Counters.DispatchedBatchCount == 0 ||
                result.WorkerTelemetry.Counters.CompletedBatchCount == 0)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.MissingWorkerTelemetry,
                    "Valid async processing results must carry completed worker telemetry.",
                    validationProfile);
            }
        }
        else if (result.WorkerTelemetry is not null &&
                 result.WorkerTelemetry.Counters.FailedBatchCount == 0 &&
                 result.WorkerTelemetry.Counters.CanceledBatchCount == 0 &&
                 result.WorkerTelemetry.Counters.RejectedDispatchCount == 0 &&
                 result.WorkerTelemetry.Counters.FailedWorkItemCount == 0 &&
                 result.WorkerTelemetry.Counters.CanceledWorkItemCount == 0)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.WorkerFailureNotPropagated,
                "Invalid async processing results with worker telemetry must report a worker failure, cancellation, or rejected dispatch.",
                validationProfile);
        }

        if (result.Telemetry is not null &&
            result.Telemetry.TopologyVersion != result.TopologyVersion)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async processing result topology version must match processing telemetry.",
                validationProfile);
        }

        var latestWorkerBatch = GetLatestWorkerBatch(result.WorkerTelemetry);
        if (latestWorkerBatch is not null &&
            latestWorkerBatch.TopologyVersion != result.TopologyVersion)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async worker telemetry topology version must match the processing result.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    private static RadarProcessingAsyncValidationResult ValidateProcessingResultDiagnostic(
        RadarProcessingResult result,
        RadarProcessingValidationProfile validationProfile)
    {
        if (result.Telemetry is not null &&
            (SumPartitionMetrics(result.Telemetry) != result.Telemetry.BatchMetrics ||
             SumShardMetrics(result.Telemetry) != result.Telemetry.BatchMetrics))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TelemetryMetricMismatch,
                "Async processing telemetry partition or shard totals do not match batch metrics.",
                validationProfile);
        }

        var latestWorkerBatch = GetLatestWorkerBatch(result.WorkerTelemetry);
        if (latestWorkerBatch is null)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        if (latestWorkerBatch.SubmittedWorkItemCount != result.ShardCount)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.MissingWorkItem,
                "Async worker telemetry submitted work item count must match the result shard count.",
                validationProfile);
        }

        if (result.IsValid &&
            (!latestWorkerBatch.IsSuccessful ||
             latestWorkerBatch.SucceededWorkItemCount != result.ShardCount))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.CompletionStatusMismatch,
                "Valid async processing results require successful completion for every shard work item.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

}
