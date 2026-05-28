using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Validates queued-provider ownership, ordering, telemetry, and reference parity.
/// </summary>
/// <remarks>
/// The validator is used as a rollout and regression guard for the owned queued
/// provider path. It checks that accepted batches retain owned payloads, provider
/// and processing sequences remain deterministic, telemetry matches evidence,
/// and optional benchmark reference metrics match the candidate run.
/// </remarks>
public static partial class RadarProcessingQueuedProviderValidator
{
    /// <summary>
    /// Validates that a batch is safe to retain in a queued provider.
    /// </summary>
    public static RadarProcessingQueuedProviderValidationResult ValidateQueuedBatch(
        RadarEventBatch batch,
        RadarProcessingQueuedProviderValidationProfile profile = RadarProcessingQueuedProviderValidationProfile.Diagnostic)
    {
        ArgumentNullException.ThrowIfNull(batch);
        EnsureKnownProfile(profile);

        if (profile == RadarProcessingQueuedProviderValidationProfile.Off)
        {
            return RadarProcessingQueuedProviderValidationResult.Valid(profile);
        }

        return batch.Lifetime == RadarEventBatchLifetime.Owned
            ? RadarProcessingQueuedProviderValidationResult.Valid(profile)
            : Invalid(
                RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch,
                "Queued provider batches must own their payload.",
                profile);
    }

    /// <summary>
    /// Validates a complete queued-provider session result against the selected profile.
    /// </summary>
    /// <remarks>
    /// Essential validation covers ownership and sequence invariants. Diagnostic
    /// validation adds telemetry and runtime-state checks. Benchmark validation can
    /// compare against a supplied reference.
    /// </remarks>
    public static RadarProcessingQueuedProviderValidationResult ValidateSessionResult(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationProfile profile = RadarProcessingQueuedProviderValidationProfile.Diagnostic,
        RadarProcessingQueuedProviderReference? reference = null,
        RadarProcessingQueuedProviderValidationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureKnownProfile(profile);

        if (profile == RadarProcessingQueuedProviderValidationProfile.Off)
        {
            return Valid(profile, context);
        }

        var essential = ValidateEssential(result, profile, context);
        if (!essential.IsValid ||
            profile == RadarProcessingQueuedProviderValidationProfile.Essential)
        {
            return essential;
        }

        var diagnostic = ValidateDiagnostic(result, profile, context);
        if (!diagnostic.IsValid ||
            profile == RadarProcessingQueuedProviderValidationProfile.Diagnostic)
        {
            return diagnostic;
        }

        return reference is null
            ? Valid(profile, context)
            : ValidateReference(result, reference, profile, context);
    }

    /// <summary>
    /// Creates deterministic comparison metrics from a queued session result.
    /// </summary>
    public static RadarProcessingQueuedProviderMetrics CreateMetrics(
        RadarProcessingQueuedSessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        ulong? validationChecksum = null;
        var payloadValueCount = 0L;
        var acceptedMoveCount = 0L;
        var skippedDecisionCount = 0L;
        var failedBatchCount = 0L;
        var failedMigrationCount = 0L;
        var workerFailedBatchCount = 0L;
        var semanticSurface = RadarProcessingQueuedProviderValidationSurface.ProcessingOnly;

        foreach (var processing in result.ProcessingResults)
        {
            ArgumentNullException.ThrowIfNull(processing);

            if (processing.ProcessingResult is { } processingResult)
            {
                validationChecksum = processingResult.Metrics.ProcessingChecksum;
                payloadValueCount = checked(payloadValueCount + processingResult.Metrics.ProcessedPayloadValueCount);
                if (processingResult.WorkerTelemetry is { } workerTelemetry)
                {
                    workerFailedBatchCount = Math.Max(
                        workerFailedBatchCount,
                        workerTelemetry.Counters.FailedBatchCount);
                }
            }

            if (IsFailedProcessingStatus(processing.Status))
            {
                failedBatchCount++;
            }

            if (processing.Status == RadarProcessingQueuedBatchProcessingStatus.FailedMigration)
            {
                failedMigrationCount++;
            }

            if (processing.RebalanceResult is not { } rebalanceResult)
            {
                continue;
            }

            semanticSurface = RadarProcessingQueuedProviderValidationSurface.Rebalance;

            if (rebalanceResult.PublishedMigration)
            {
                acceptedMoveCount++;
            }

            skippedDecisionCount = checked(skippedDecisionCount + CountSkippedDecisions(rebalanceResult));
        }

        return new RadarProcessingQueuedProviderMetrics(
            validationChecksum,
            payloadValueCount,
            acceptedMoveCount,
            skippedDecisionCount,
            failedBatchCount,
            failedMigrationCount,
            workerFailedBatchCount,
            semanticSurface);
    }

    internal static void EnsureKnownProfile(
        RadarProcessingQueuedProviderValidationProfile profile)
    {
        if (profile is not RadarProcessingQueuedProviderValidationProfile.Off and
            not RadarProcessingQueuedProviderValidationProfile.Essential and
            not RadarProcessingQueuedProviderValidationProfile.Diagnostic and
            not RadarProcessingQueuedProviderValidationProfile.Benchmark)
        {
            throw new ArgumentOutOfRangeException(nameof(profile));
        }
    }

}
