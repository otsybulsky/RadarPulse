namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedProviderValidationError
{
    None = 0,
    NonOwnedQueuedBatch = 1,
    ProviderSequenceRegression = 2,
    ProcessingSequenceRegression = 3,
    MissingCompletionForAcceptedBatch = 4,
    TopologyVersionRegression = 5,
    TelemetryCounterMismatch = 6,
    QueueFaultStateMismatch = 7,
    WorkerFailureCountMismatch = 8,
    DeterministicChecksumMismatch = 9,
    AcceptedMoveCountMismatch = 10,
    SkippedDecisionCountMismatch = 11,
    FailureCountMismatch = 12,
    FinalTopologyVersionMismatch = 13,
    ProviderSequenceGap = 14,
    ProcessingSequenceGap = 15,
    PayloadValueCountMismatch = 16,
    FailedMigrationCountMismatch = 17,
    ReferenceSemanticSurfaceMismatch = 18,
    RetentionTelemetryIncomplete = 19,
    RetentionTelemetryMismatch = 20,
    RetainedResourceCleanupIncomplete = 21,
    OverlapTelemetryIncomplete = 22
}
