namespace RadarPulse.Domain.Processing;

public enum RadarProcessingAsyncValidationError
{
    None = 0,
    NonAsyncExecutionMode = 1,
    MissingWorkerTelemetry = 2,
    MissingProcessingTelemetry = 3,
    FailedBatchCompletion = 4,
    IncompleteBatchCompletion = 5,
    WorkerFailureNotPropagated = 6,
    TopologyVersionMismatch = 7,
    UnexpectedMigrationAfterFailedProcessing = 8,
    MissingWorkItem = 9,
    DuplicateWorkAssignment = 10,
    WorkItemScopeMismatch = 11,
    WorkItemShardOwnershipMismatch = 12,
    WorkItemWorkerAssignmentMismatch = 13,
    CompletionScopeMismatch = 14,
    CompletionStatusMismatch = 15,
    AggregationMetricMismatch = 16,
    TelemetryMetricMismatch = 17,
    DeterministicChecksumMismatch = 18,
    RetentionLimitExceeded = 19
}
