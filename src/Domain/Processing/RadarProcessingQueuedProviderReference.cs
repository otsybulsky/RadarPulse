namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQueuedProviderReference
{
    public RadarProcessingQueuedProviderReference(
        ulong? validationChecksum = null,
        long? acceptedMoveCount = null,
        long? skippedDecisionCount = null,
        long? failedBatchCount = null,
        long? workerFailedBatchCount = null,
        RadarProcessingTopologyVersion? finalTopologyVersion = null)
    {
        ThrowIfNegative(acceptedMoveCount, nameof(acceptedMoveCount));
        ThrowIfNegative(skippedDecisionCount, nameof(skippedDecisionCount));
        ThrowIfNegative(failedBatchCount, nameof(failedBatchCount));
        ThrowIfNegative(workerFailedBatchCount, nameof(workerFailedBatchCount));

        ValidationChecksum = validationChecksum;
        AcceptedMoveCount = acceptedMoveCount;
        SkippedDecisionCount = skippedDecisionCount;
        FailedBatchCount = failedBatchCount;
        WorkerFailedBatchCount = workerFailedBatchCount;
        FinalTopologyVersion = finalTopologyVersion;
    }

    public ulong? ValidationChecksum { get; }

    public long? AcceptedMoveCount { get; }

    public long? SkippedDecisionCount { get; }

    public long? FailedBatchCount { get; }

    public long? WorkerFailedBatchCount { get; }

    public RadarProcessingTopologyVersion? FinalTopologyVersion { get; }

    public static RadarProcessingQueuedProviderReference FromQueuedSession(
        RadarProcessingQueuedSessionResult result,
        ulong? validationChecksum = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var metrics = RadarProcessingQueuedProviderValidator.CreateMetrics(result);
        return new RadarProcessingQueuedProviderReference(
            validationChecksum ?? metrics.ValidationChecksum,
            metrics.AcceptedMoveCount,
            metrics.SkippedDecisionCount,
            metrics.FailedBatchCount,
            metrics.WorkerFailedBatchCount,
            result.FinalTopologyVersion);
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
