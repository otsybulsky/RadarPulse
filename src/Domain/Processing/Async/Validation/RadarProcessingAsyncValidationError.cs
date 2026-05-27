namespace RadarPulse.Domain.Processing;

/// <summary>
/// Identifies async processing validation failures.
/// </summary>
public enum RadarProcessingAsyncValidationError
{
    /// <summary>
    /// No async validation error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validation expected async shard transport but received another execution mode.
    /// </summary>
    NonAsyncExecutionMode = 1,

    /// <summary>
    /// Async processing did not include required worker telemetry.
    /// </summary>
    MissingWorkerTelemetry = 2,

    /// <summary>
    /// Async processing did not include required partition or shard telemetry.
    /// </summary>
    MissingProcessingTelemetry = 3,

    /// <summary>
    /// Batch completion reported failed work.
    /// </summary>
    FailedBatchCompletion = 4,

    /// <summary>
    /// Batch completion did not include every expected work item.
    /// </summary>
    IncompleteBatchCompletion = 5,

    /// <summary>
    /// Worker failure evidence was not propagated to the processing result.
    /// </summary>
    WorkerFailureNotPropagated = 6,

    /// <summary>
    /// Topology versions did not match across route, result, telemetry, or completion evidence.
    /// </summary>
    TopologyVersionMismatch = 7,

    /// <summary>
    /// Rebalance artifacts were produced after processing failed.
    /// </summary>
    UnexpectedMigrationAfterFailedProcessing = 8,

    /// <summary>
    /// Work items did not cover the expected route or completion scope.
    /// </summary>
    MissingWorkItem = 9,

    /// <summary>
    /// A route partition was assigned to more than one async work item.
    /// </summary>
    DuplicateWorkAssignment = 10,

    /// <summary>
    /// A work item did not belong to the route or batch scope being validated.
    /// </summary>
    WorkItemScopeMismatch = 11,

    /// <summary>
    /// A work item referenced partitions outside its shard ownership.
    /// </summary>
    WorkItemShardOwnershipMismatch = 12,

    /// <summary>
    /// A work item referenced a worker outside the configured worker range.
    /// </summary>
    WorkItemWorkerAssignmentMismatch = 13,

    /// <summary>
    /// A completion did not match its submitted work item.
    /// </summary>
    CompletionScopeMismatch = 14,

    /// <summary>
    /// Completion status did not satisfy the successful validation contract.
    /// </summary>
    CompletionStatusMismatch = 15,

    /// <summary>
    /// Aggregated completion metrics did not match routed shard metrics.
    /// </summary>
    AggregationMetricMismatch = 16,

    /// <summary>
    /// Processing telemetry partition or shard totals did not match batch metrics.
    /// </summary>
    TelemetryMetricMismatch = 17,

    /// <summary>
    /// Async and synchronous deterministic outputs diverged.
    /// </summary>
    DeterministicChecksumMismatch = 18,

    /// <summary>
    /// Retained worker telemetry exceeded configured retention limits.
    /// </summary>
    RetentionLimitExceeded = 19
}
