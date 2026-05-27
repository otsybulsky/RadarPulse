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
public static class RadarProcessingQueuedProviderValidator
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

    private static RadarProcessingQueuedProviderValidationResult ValidateEssential(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context)
    {
        var acceptedSequences = new HashSet<long>();
        long? previousProviderSequence = null;
        var expectedProviderSequence = 0L;

        foreach (var enqueue in result.EnqueueResults)
        {
            RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(enqueue.Status);
            if (!enqueue.IsAccepted)
            {
                continue;
            }

            var batch = enqueue.Batch ??
                        throw new InvalidOperationException("Accepted enqueue results must carry a queued batch.");
            if (batch.Batch.Lifetime != RadarEventBatchLifetime.Owned)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch,
                    "Queued provider batches must own their payload.",
                    profile,
                    context: context);
            }

            var sequence = batch.Sequence.Value;
            if (sequence > expectedProviderSequence)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProviderSequenceGap,
                    "Accepted provider sequence ids must be contiguous.",
                    profile,
                    context: context,
                    expectedCount: expectedProviderSequence,
                    actualCount: sequence);
            }

            if (previousProviderSequence.HasValue && sequence <= previousProviderSequence.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProviderSequenceRegression,
                    "Accepted provider sequence ids must increase monotonically.",
                    profile,
                    context: context);
            }

            previousProviderSequence = sequence;
            acceptedSequences.Add(sequence);
            expectedProviderSequence = checked(sequence + 1);
        }

        var processedSequences = new HashSet<long>();
        long? previousProcessingSequence = null;
        RadarProcessingTopologyVersion? previousTopologyVersion = null;
        foreach (var processing in result.ProcessingResults)
        {
            RadarProcessingQueuedBatchProcessingResult.EnsureKnownStatus(processing.Status);
            var sequence = processing.Sequence.Value;
            if (previousProcessingSequence.HasValue && sequence <= previousProcessingSequence.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProcessingSequenceRegression,
                    "Processed provider sequence ids must increase monotonically.",
                    profile,
                    context: context);
            }

            previousProcessingSequence = sequence;
            processedSequences.Add(sequence);

            if (processing.TopologyVersion is not { } topologyVersion)
            {
                continue;
            }

            if (previousTopologyVersion.HasValue &&
                topologyVersion.Value < previousTopologyVersion.Value.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.TopologyVersionRegression,
                    "Queued processing topology versions must not regress.",
                    profile,
                    context: context);
            }

            previousTopologyVersion = topologyVersion;
            if (result.FinalTopologyVersion.HasValue &&
                topologyVersion.Value > result.FinalTopologyVersion.Value.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.TopologyVersionRegression,
                    "Final queued topology version must cover every processed topology version.",
                    profile,
                    context: context);
            }
        }

        if (!result.IsCanceled)
        {
            foreach (var sequence in acceptedSequences.OrderBy(static sequence => sequence))
            {
                if (!processedSequences.Contains(sequence))
                {
                    var nextProcessedSequence = processedSequences
                        .Where(processedSequence => processedSequence > sequence)
                        .DefaultIfEmpty(-1)
                        .Min();
                    if (nextProcessedSequence >= 0)
                    {
                        return Invalid(
                            RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap,
                            "Processed provider sequence ids must be contiguous.",
                            profile,
                            context: context,
                            expectedCount: sequence,
                            actualCount: nextProcessedSequence);
                    }

                    return Invalid(
                        RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch,
                        "Every accepted provider sequence requires a processing result unless the session is canceled.",
                        profile,
                        context: context);
                }
            }

            foreach (var sequence in processedSequences.OrderBy(static sequence => sequence))
            {
                if (!acceptedSequences.Contains(sequence))
                {
                    return Invalid(
                        RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap,
                        "Processed provider sequence ids must not include unaccepted provider sequence ids.",
                        profile,
                        context: context,
                        expectedCount: expectedProviderSequence,
                        actualCount: sequence);
                }
            }
        }

        return Valid(profile, context);
    }

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

    private static RadarProcessingQueuedProviderValidationResult ValidateReference(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderReference reference,
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context)
    {
        var metrics = CreateMetrics(result);
        if (context is not null &&
            reference.SemanticSurface.HasValue &&
            context.SemanticSurface != reference.SemanticSurface.Value)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch,
                "Queued provider validation semantic surface does not match the borrowed blocking reference.",
                profile,
                context: context,
                expectedCount: (int)reference.SemanticSurface.Value,
                actualCount: (int)context.SemanticSurface);
        }

        if (reference.ValidationChecksum.HasValue &&
            reference.ValidationChecksum != metrics.ValidationChecksum)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch,
                "Queued provider checksum does not match the borrowed blocking reference.",
                profile,
                expectedChecksum: reference.ValidationChecksum,
                actualChecksum: metrics.ValidationChecksum,
                context: context);
        }

        if (reference.PayloadValueCount.HasValue &&
            reference.PayloadValueCount != metrics.PayloadValueCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.PayloadValueCountMismatch,
                "Queued provider payload value count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.PayloadValueCount,
                actualCount: metrics.PayloadValueCount,
                context: context);
        }

        if (reference.AcceptedMoveCount.HasValue &&
            reference.AcceptedMoveCount != metrics.AcceptedMoveCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch,
                "Queued provider accepted move count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.AcceptedMoveCount,
                actualCount: metrics.AcceptedMoveCount,
                context: context);
        }

        if (reference.SkippedDecisionCount.HasValue &&
            reference.SkippedDecisionCount != metrics.SkippedDecisionCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch,
                "Queued provider skipped decision count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.SkippedDecisionCount,
                actualCount: metrics.SkippedDecisionCount,
                context: context);
        }

        if (reference.FailedBatchCount.HasValue &&
            reference.FailedBatchCount != metrics.FailedBatchCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.FailureCountMismatch,
                "Queued provider failed batch count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.FailedBatchCount,
                actualCount: metrics.FailedBatchCount,
                context: context);
        }

        if (reference.FailedMigrationCount.HasValue &&
            reference.FailedMigrationCount != metrics.FailedMigrationCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch,
                "Queued provider failed migration count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.FailedMigrationCount,
                actualCount: metrics.FailedMigrationCount,
                context: context);
        }

        if (reference.WorkerFailedBatchCount.HasValue &&
            reference.WorkerFailedBatchCount != metrics.WorkerFailedBatchCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.WorkerFailureCountMismatch,
                "Queued provider worker failure count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.WorkerFailedBatchCount,
                actualCount: metrics.WorkerFailedBatchCount,
                context: context);
        }

        if (reference.FinalTopologyVersion.HasValue &&
            reference.FinalTopologyVersion != result.FinalTopologyVersion)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch,
                "Queued provider final topology version does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.FinalTopologyVersion.Value.Value,
                actualCount: result.FinalTopologyVersion?.Value,
                context: context);
        }

        return Valid(profile, context);
    }

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

    private static bool IsFailedProcessingStatus(
        RadarProcessingQueuedBatchProcessingStatus status) =>
        status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration;

    private static long CountSkippedDecisions(
        RadarProcessingRebalanceSessionResult result)
    {
        var count = 0L;
        if (result.DirectHotReliefDecision is { HasAcceptedMove: false })
        {
            count++;
        }

        if (result.ColdEvacuationDecision is { HasAcceptedMove: false })
        {
            count++;
        }

        return count;
    }

    private static RadarProcessingQueuedProviderValidationResult Invalid(
        RadarProcessingQueuedProviderValidationError error,
        string message,
        RadarProcessingQueuedProviderValidationProfile profile,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null,
        RadarProcessingQueuedProviderValidationContext? context = null) =>
        RadarProcessingQueuedProviderValidationResult.Invalid(
            error,
            message,
            profile,
            expectedChecksum,
            actualChecksum,
            expectedCount,
            actualCount,
            context);

    private static RadarProcessingQueuedProviderValidationResult Valid(
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context) =>
        RadarProcessingQueuedProviderValidationResult.Valid(profile, context);
}
