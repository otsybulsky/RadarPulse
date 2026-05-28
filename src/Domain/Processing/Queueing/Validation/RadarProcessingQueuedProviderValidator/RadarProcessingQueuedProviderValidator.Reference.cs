using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderValidator
{
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

}
