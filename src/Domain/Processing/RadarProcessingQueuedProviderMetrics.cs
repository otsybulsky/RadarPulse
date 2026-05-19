namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingQueuedProviderMetrics(
    ulong? ValidationChecksum,
    long AcceptedMoveCount,
    long SkippedDecisionCount,
    long FailedBatchCount,
    long WorkerFailedBatchCount);
