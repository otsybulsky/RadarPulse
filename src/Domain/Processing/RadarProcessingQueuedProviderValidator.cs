using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static class RadarProcessingQueuedProviderValidator
{
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

    public static RadarProcessingQueuedProviderValidationResult ValidateSessionResult(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationProfile profile = RadarProcessingQueuedProviderValidationProfile.Diagnostic,
        RadarProcessingQueuedProviderReference? reference = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureKnownProfile(profile);

        if (profile == RadarProcessingQueuedProviderValidationProfile.Off)
        {
            return RadarProcessingQueuedProviderValidationResult.Valid(profile);
        }

        var essential = ValidateEssential(result, profile);
        if (!essential.IsValid ||
            profile == RadarProcessingQueuedProviderValidationProfile.Essential)
        {
            return essential;
        }

        var diagnostic = ValidateDiagnostic(result, profile);
        if (!diagnostic.IsValid ||
            profile == RadarProcessingQueuedProviderValidationProfile.Diagnostic)
        {
            return diagnostic;
        }

        return reference is null
            ? RadarProcessingQueuedProviderValidationResult.Valid(profile)
            : ValidateReference(result, reference, profile);
    }

    public static RadarProcessingQueuedProviderMetrics CreateMetrics(
        RadarProcessingQueuedSessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        ulong? validationChecksum = null;
        var acceptedMoveCount = 0L;
        var skippedDecisionCount = 0L;
        var failedBatchCount = 0L;
        var workerFailedBatchCount = 0L;

        foreach (var processing in result.ProcessingResults)
        {
            ArgumentNullException.ThrowIfNull(processing);

            if (processing.ProcessingResult is { } processingResult)
            {
                validationChecksum = processingResult.Metrics.ProcessingChecksum;
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

            if (processing.RebalanceResult is not { } rebalanceResult)
            {
                continue;
            }

            if (rebalanceResult.PublishedMigration)
            {
                acceptedMoveCount++;
            }

            skippedDecisionCount = checked(skippedDecisionCount + CountSkippedDecisions(rebalanceResult));
        }

        return new RadarProcessingQueuedProviderMetrics(
            validationChecksum,
            acceptedMoveCount,
            skippedDecisionCount,
            failedBatchCount,
            workerFailedBatchCount);
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
        RadarProcessingQueuedProviderValidationProfile profile)
    {
        var acceptedSequences = new HashSet<long>();
        long? previousProviderSequence = null;

        foreach (var enqueue in result.EnqueueResults)
        {
            RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(enqueue.Status);
            if (!enqueue.IsAccepted)
            {
                continue;
            }

            var batch = enqueue.Batch ??
                        throw new InvalidOperationException("Accepted enqueue results must carry a queued batch.");
            var ownedValidation = ValidateQueuedBatch(batch.Batch, profile);
            if (!ownedValidation.IsValid)
            {
                return ownedValidation;
            }

            var sequence = batch.Sequence.Value;
            if (previousProviderSequence.HasValue && sequence <= previousProviderSequence.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.ProviderSequenceRegression,
                    "Accepted provider sequence ids must increase monotonically.",
                    profile);
            }

            previousProviderSequence = sequence;
            acceptedSequences.Add(sequence);
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
                    profile);
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
                    profile);
            }

            previousTopologyVersion = topologyVersion;
            if (result.FinalTopologyVersion.HasValue &&
                topologyVersion.Value > result.FinalTopologyVersion.Value.Value)
            {
                return Invalid(
                    RadarProcessingQueuedProviderValidationError.TopologyVersionRegression,
                    "Final queued topology version must cover every processed topology version.",
                    profile);
            }
        }

        if (!result.IsCanceled)
        {
            foreach (var sequence in acceptedSequences)
            {
                if (!processedSequences.Contains(sequence))
                {
                    return Invalid(
                        RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch,
                        "Every accepted provider sequence requires a processing result unless the session is canceled.",
                        profile);
                }
            }
        }

        return RadarProcessingQueuedProviderValidationResult.Valid(profile);
    }

    private static RadarProcessingQueuedProviderValidationResult ValidateDiagnostic(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderValidationProfile profile)
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
                profile);
        }

        if (result.IsCompleted && failed > 0)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.QueueFaultStateMismatch,
                "Completed queued provider sessions must not contain failed processing results.",
                profile);
        }

        var metrics = CreateMetrics(result);
        if (metrics.WorkerFailedBatchCount > 0 && failed == 0 && canceled == 0)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.WorkerFailureCountMismatch,
                "Worker failure telemetry must be reflected by failed or canceled queued batch results.",
                profile);
        }

        return RadarProcessingQueuedProviderValidationResult.Valid(profile);
    }

    private static RadarProcessingQueuedProviderValidationResult ValidateReference(
        RadarProcessingQueuedSessionResult result,
        RadarProcessingQueuedProviderReference reference,
        RadarProcessingQueuedProviderValidationProfile profile)
    {
        var metrics = CreateMetrics(result);
        if (reference.ValidationChecksum.HasValue &&
            reference.ValidationChecksum != metrics.ValidationChecksum)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch,
                "Queued provider checksum does not match the borrowed blocking reference.",
                profile,
                expectedChecksum: reference.ValidationChecksum,
                actualChecksum: metrics.ValidationChecksum);
        }

        if (reference.AcceptedMoveCount.HasValue &&
            reference.AcceptedMoveCount != metrics.AcceptedMoveCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch,
                "Queued provider accepted move count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.AcceptedMoveCount,
                actualCount: metrics.AcceptedMoveCount);
        }

        if (reference.SkippedDecisionCount.HasValue &&
            reference.SkippedDecisionCount != metrics.SkippedDecisionCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch,
                "Queued provider skipped decision count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.SkippedDecisionCount,
                actualCount: metrics.SkippedDecisionCount);
        }

        if (reference.FailedBatchCount.HasValue &&
            reference.FailedBatchCount != metrics.FailedBatchCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.FailureCountMismatch,
                "Queued provider failed batch count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.FailedBatchCount,
                actualCount: metrics.FailedBatchCount);
        }

        if (reference.WorkerFailedBatchCount.HasValue &&
            reference.WorkerFailedBatchCount != metrics.WorkerFailedBatchCount)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.WorkerFailureCountMismatch,
                "Queued provider worker failure count does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.WorkerFailedBatchCount,
                actualCount: metrics.WorkerFailedBatchCount);
        }

        if (reference.FinalTopologyVersion.HasValue &&
            reference.FinalTopologyVersion != result.FinalTopologyVersion)
        {
            return Invalid(
                RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch,
                "Queued provider final topology version does not match the borrowed blocking reference.",
                profile,
                expectedCount: reference.FinalTopologyVersion.Value.Value,
                actualCount: result.FinalTopologyVersion?.Value);
        }

        return RadarProcessingQueuedProviderValidationResult.Valid(profile);
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
        long? actualCount = null) =>
        RadarProcessingQueuedProviderValidationResult.Invalid(
            error,
            message,
            profile,
            expectedChecksum,
            actualChecksum,
            expectedCount,
            actualCount);
}
