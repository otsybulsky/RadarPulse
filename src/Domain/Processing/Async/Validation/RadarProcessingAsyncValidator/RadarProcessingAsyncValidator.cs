namespace RadarPulse.Domain.Processing;

/// <summary>
/// Validates async processing results, transport assignment, deterministic parity, and retained worker telemetry.
/// </summary>
public static partial class RadarProcessingAsyncValidator
{
    /// <summary>
    /// Validates an async shard transport processing result according to the selected profile.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateProcessingResult(
        RadarProcessingResult result,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(result);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        if (result.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.NonAsyncExecutionMode,
                "Async validation requires an async shard transport processing result.",
                validationProfile);
        }

        var essential = ValidateProcessingResultEssential(result, validationProfile);
        if (!essential.IsValid ||
            validationProfile == RadarProcessingValidationProfile.Essential)
        {
            return essential;
        }

        var diagnostic = ValidateProcessingResultDiagnostic(result, validationProfile);
        return diagnostic.IsValid ||
               validationProfile == RadarProcessingValidationProfile.Diagnostic ||
               validationProfile == RadarProcessingValidationProfile.Benchmark
            ? diagnostic
            : RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    /// <summary>
    /// Validates an async rebalance result and ensures failed processing does not publish migration artifacts.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateRebalanceResult(
        RadarProcessingRebalanceSessionResult result,
        RadarProcessingTopology? currentTopology = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(result);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        var processing = ValidateProcessingResult(result.ProcessingResult, validationProfile);
        if (!processing.IsValid)
        {
            return processing;
        }

        if (!result.ProcessingResult.IsValid &&
            (result.PressureSample is not null ||
             result.DirectHotReliefDecision is not null ||
             result.ColdEvacuationDecision is not null ||
             result.MigrationResult is not null ||
             result.HandoffValidation is not null ||
             result.PublishedMigration))
        {
            return Invalid(
                RadarProcessingAsyncValidationError.UnexpectedMigrationAfterFailedProcessing,
                "Failed async processing must not evaluate or publish rebalance artifacts.",
                validationProfile);
        }

        if (currentTopology is not null &&
            result.MigrationResult is not null &&
            result.MigrationResult.CurrentTopologyVersion != currentTopology.Version)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.TopologyVersionMismatch,
                "Async rebalance migration topology version must match the current topology.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

    /// <summary>
    /// Validates async work item coverage, completion scope, worker assignment, and aggregate metrics.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateTransport(
        RadarProcessingBatchRoute route,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems,
        RadarProcessingAsyncBatchScopeResult? batchResult,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic,
        int? workerCount = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(workItems);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);
        if (workerCount.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount.Value);
        }

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        var essential = ValidateTransportEssential(route, workItems, batchResult, validationProfile);
        if (!essential.IsValid ||
            validationProfile == RadarProcessingValidationProfile.Essential)
        {
            return essential;
        }

        return ValidateTransportDiagnostic(route, workItems, batchResult!, validationProfile, workerCount);
    }

    /// <summary>
    /// Compares synchronous and async outputs for benchmark-grade deterministic parity.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateBenchmarkComparison(
        RadarProcessingResult synchronousResult,
        RadarProcessingResult asyncResult,
        IReadOnlyList<RadarSourceProcessingSnapshot>? synchronousSnapshots = null,
        IReadOnlyList<RadarSourceProcessingSnapshot>? asyncSnapshots = null,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Benchmark)
    {
        ArgumentNullException.ThrowIfNull(synchronousResult);
        ArgumentNullException.ThrowIfNull(asyncResult);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile != RadarProcessingValidationProfile.Benchmark)
        {
            return ValidateProcessingResult(asyncResult, validationProfile);
        }

        if (synchronousResult.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.NonAsyncExecutionMode,
                "Benchmark comparison requires a synchronous reference result.",
                validationProfile,
                synchronousResult.Metrics.ProcessingChecksum,
                asyncResult.Metrics.ProcessingChecksum);
        }

        var asyncValidation = ValidateProcessingResult(asyncResult, validationProfile);
        if (!asyncValidation.IsValid)
        {
            return asyncValidation;
        }

        if (synchronousResult.IsValid != asyncResult.IsValid ||
            synchronousResult.Metrics != asyncResult.Metrics)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.DeterministicChecksumMismatch,
                "Synchronous and async processing metrics do not match.",
                validationProfile,
                synchronousResult.Metrics.ProcessingChecksum,
                asyncResult.Metrics.ProcessingChecksum);
        }

        if (synchronousSnapshots is not null || asyncSnapshots is not null)
        {
            if (synchronousSnapshots is null ||
                asyncSnapshots is null ||
                synchronousSnapshots.Count != asyncSnapshots.Count)
            {
                return Invalid(
                    RadarProcessingAsyncValidationError.DeterministicChecksumMismatch,
                    "Synchronous and async processing snapshot counts do not match.",
                    validationProfile,
                    synchronousResult.Metrics.ProcessingChecksum,
                    asyncResult.Metrics.ProcessingChecksum);
            }

            for (var sourceId = 0; sourceId < synchronousSnapshots.Count; sourceId++)
            {
                if (synchronousSnapshots[sourceId] != asyncSnapshots[sourceId])
                {
                    return Invalid(
                        RadarProcessingAsyncValidationError.DeterministicChecksumMismatch,
                        "Synchronous and async source snapshots do not match.",
                        validationProfile,
                        synchronousResult.Metrics.ProcessingChecksum,
                        asyncResult.Metrics.ProcessingChecksum);
                }
            }
        }

        return RadarProcessingAsyncValidationResult.Valid(
            validationProfile,
            synchronousResult.Metrics.ProcessingChecksum,
            asyncResult.Metrics.ProcessingChecksum);
    }

    /// <summary>
    /// Validates that retained worker telemetry obeys configured retention limits and counters.
    /// </summary>
    public static RadarProcessingAsyncValidationResult ValidateWorkerTelemetryRetention(
        RadarProcessingWorkerTelemetrySummary workerTelemetry,
        RadarProcessingTelemetryRetentionOptions retentionOptions,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(workerTelemetry);
        ArgumentNullException.ThrowIfNull(retentionOptions);
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);

        if (validationProfile == RadarProcessingValidationProfile.Off)
        {
            return RadarProcessingAsyncValidationResult.Valid(validationProfile);
        }

        var expectedBatchLimit = retentionOptions.RetentionMode == RadarProcessingDiagnosticRetentionMode.Counters
            ? 0
            : retentionOptions.MaxRetainedWorkerBatches;
        var expectedFailureLimit = retentionOptions.RetentionMode == RadarProcessingDiagnosticRetentionMode.Counters
            ? 0
            : retentionOptions.MaxRetainedWorkerFailures;

        if (workerTelemetry.RecentBatches.Count > expectedBatchLimit ||
            workerTelemetry.RecentFailures.Count > expectedFailureLimit)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.RetentionLimitExceeded,
                "Async worker telemetry detail exceeds the configured retention limits.",
                validationProfile);
        }

        if (workerTelemetry.RetentionStats.RetainedBatchCount != workerTelemetry.RecentBatches.Count ||
            workerTelemetry.RetentionStats.RetainedFailureCount != workerTelemetry.RecentFailures.Count)
        {
            return Invalid(
                RadarProcessingAsyncValidationError.RetentionLimitExceeded,
                "Async worker telemetry retention stats do not match retained detail counts.",
                validationProfile);
        }

        return RadarProcessingAsyncValidationResult.Valid(validationProfile);
    }

}
