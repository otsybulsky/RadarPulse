namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingQueuedProviderMetrics(
    ulong? ValidationChecksum,
    long PayloadValueCount,
    long AcceptedMoveCount,
    long SkippedDecisionCount,
    long FailedBatchCount,
    long FailedMigrationCount,
    long WorkerFailedBatchCount,
    RadarProcessingQueuedProviderValidationSurface SemanticSurface);
