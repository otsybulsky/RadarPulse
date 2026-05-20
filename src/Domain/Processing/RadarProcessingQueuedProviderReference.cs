namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQueuedProviderReference
{
    public RadarProcessingQueuedProviderReference(
        ulong? validationChecksum = null,
        long? payloadValueCount = null,
        long? acceptedMoveCount = null,
        long? skippedDecisionCount = null,
        long? failedBatchCount = null,
        long? failedMigrationCount = null,
        long? workerFailedBatchCount = null,
        RadarProcessingTopologyVersion? finalTopologyVersion = null,
        RadarProcessingQueuedProviderValidationSurface? semanticSurface = null)
    {
        ThrowIfNegative(payloadValueCount, nameof(payloadValueCount));
        ThrowIfNegative(acceptedMoveCount, nameof(acceptedMoveCount));
        ThrowIfNegative(skippedDecisionCount, nameof(skippedDecisionCount));
        ThrowIfNegative(failedBatchCount, nameof(failedBatchCount));
        ThrowIfNegative(failedMigrationCount, nameof(failedMigrationCount));
        ThrowIfNegative(workerFailedBatchCount, nameof(workerFailedBatchCount));
        if (semanticSurface.HasValue)
        {
            RadarProcessingQueuedProviderValidationContext.EnsureKnownSurface(semanticSurface.Value);
        }

        ValidationChecksum = validationChecksum;
        PayloadValueCount = payloadValueCount;
        AcceptedMoveCount = acceptedMoveCount;
        SkippedDecisionCount = skippedDecisionCount;
        FailedBatchCount = failedBatchCount;
        FailedMigrationCount = failedMigrationCount;
        WorkerFailedBatchCount = workerFailedBatchCount;
        FinalTopologyVersion = finalTopologyVersion;
        SemanticSurface = semanticSurface;
    }

    public ulong? ValidationChecksum { get; }

    public long? PayloadValueCount { get; }

    public long? AcceptedMoveCount { get; }

    public long? SkippedDecisionCount { get; }

    public long? FailedBatchCount { get; }

    public long? FailedMigrationCount { get; }

    public long? WorkerFailedBatchCount { get; }

    public RadarProcessingTopologyVersion? FinalTopologyVersion { get; }

    public RadarProcessingQueuedProviderValidationSurface? SemanticSurface { get; }

    public static RadarProcessingQueuedProviderReference FromQueuedSession(
        RadarProcessingQueuedSessionResult result,
        ulong? validationChecksum = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var metrics = RadarProcessingQueuedProviderValidator.CreateMetrics(result);
        return new RadarProcessingQueuedProviderReference(
            validationChecksum: validationChecksum ?? metrics.ValidationChecksum,
            payloadValueCount: metrics.PayloadValueCount,
            acceptedMoveCount: metrics.AcceptedMoveCount,
            skippedDecisionCount: metrics.SkippedDecisionCount,
            failedBatchCount: metrics.FailedBatchCount,
            failedMigrationCount: metrics.FailedMigrationCount,
            workerFailedBatchCount: metrics.WorkerFailedBatchCount,
            finalTopologyVersion: result.FinalTopologyVersion,
            semanticSurface: metrics.SemanticSurface);
    }

    private static void ThrowIfNegative(
        long? value,
        string paramName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
