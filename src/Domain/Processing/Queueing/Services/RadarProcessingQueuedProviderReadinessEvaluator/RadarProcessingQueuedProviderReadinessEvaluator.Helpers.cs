namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingQueuedProviderReadinessEvaluator
{
    private static RadarProcessingQueuedProviderReadinessGate SelectCorrectnessGate(
        RadarProcessingQueuedProviderValidationError error) =>
        error is RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch or
            RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch or
            RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch or
            RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch or
            RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch
            ? RadarProcessingQueuedProviderReadinessGate.TopologyAndRebalanceParity
            : RadarProcessingQueuedProviderReadinessGate.CorrectnessParity;

    private static RadarProcessingQueuedProviderReadinessError SelectCorrectnessError(
        RadarProcessingQueuedProviderValidationError error) =>
        error switch
        {
            RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch =>
                RadarProcessingQueuedProviderReadinessError.ChecksumMismatch,
            RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch or
            RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch or
            RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch or
            RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch or
            RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch =>
                RadarProcessingQueuedProviderReadinessError.TopologyOrRebalanceMismatch,
            _ => RadarProcessingQueuedProviderReadinessError.QueuedProviderValidationFailed
        };

    private static void EnsureNonNegative(
        TimeSpan value,
        string paramName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
