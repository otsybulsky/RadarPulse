using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderValidator
{
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
