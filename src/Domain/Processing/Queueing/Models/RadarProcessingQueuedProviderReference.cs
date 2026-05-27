namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reference metrics for comparing a queued-provider run with an accepted baseline.
/// </summary>
/// <remarks>
/// The reference keeps only deterministic semantic evidence: payload counts,
/// validation checksum, failure/move counters, final topology, and validation
/// surface. It is used by rollout and readiness gates without retaining full
/// processing results.
/// </remarks>
public sealed record RadarProcessingQueuedProviderReference
{
    /// <summary>
    /// Creates an optional reference metric set.
    /// </summary>
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

    /// <summary>
    /// Deterministic checksum over the validation surface when available.
    /// </summary>
    public ulong? ValidationChecksum { get; }

    /// <summary>
    /// Payload value count observed by the reference run.
    /// </summary>
    public long? PayloadValueCount { get; }

    /// <summary>
    /// Accepted rebalance move count observed by the reference run.
    /// </summary>
    public long? AcceptedMoveCount { get; }

    /// <summary>
    /// Skipped rebalance decision count observed by the reference run.
    /// </summary>
    public long? SkippedDecisionCount { get; }

    /// <summary>
    /// Failed batch count observed by the reference run.
    /// </summary>
    public long? FailedBatchCount { get; }

    /// <summary>
    /// Failed migration count observed by the reference run.
    /// </summary>
    public long? FailedMigrationCount { get; }

    /// <summary>
    /// Worker-level failed batch count observed by the reference run.
    /// </summary>
    public long? WorkerFailedBatchCount { get; }

    /// <summary>
    /// Final topology version reached by the reference run.
    /// </summary>
    public RadarProcessingTopologyVersion? FinalTopologyVersion { get; }

    /// <summary>
    /// Semantic surface covered by the reference metrics.
    /// </summary>
    public RadarProcessingQueuedProviderValidationSurface? SemanticSurface { get; }

    /// <summary>
    /// Builds reference metrics from a queued session result.
    /// </summary>
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
