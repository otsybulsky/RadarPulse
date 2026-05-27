namespace RadarPulse.Domain.Processing;

/// <summary>
/// Deterministic metrics extracted from a queued-provider session for parity checks.
/// </summary>
/// <remarks>
/// Metrics intentionally avoid retaining full run output. They carry only the
/// checksum, counts, failure counters, and semantic surface needed by validation
/// and rollout readiness gates.
/// </remarks>
public readonly record struct RadarProcessingQueuedProviderMetrics(
    ulong? ValidationChecksum,
    long PayloadValueCount,
    long AcceptedMoveCount,
    long SkippedDecisionCount,
    long FailedBatchCount,
    long FailedMigrationCount,
    long WorkerFailedBatchCount,
    RadarProcessingQueuedProviderValidationSurface SemanticSurface);
