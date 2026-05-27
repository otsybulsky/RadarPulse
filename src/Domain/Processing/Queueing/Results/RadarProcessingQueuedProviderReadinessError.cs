namespace RadarPulse.Domain.Processing;

/// <summary>
/// Error classification for queued-provider rollout readiness gates.
/// </summary>
public enum RadarProcessingQueuedProviderReadinessError
{
    /// <summary>
    /// No readiness error.
    /// </summary>
    None = 0,

    /// <summary>
    /// The gate was intentionally not evaluated.
    /// </summary>
    NotEvaluated = 1,

    /// <summary>
    /// Required borrowed/baseline reference evidence is missing.
    /// </summary>
    MissingBorrowedReference = 2,

    /// <summary>
    /// Queued-provider validation failed.
    /// </summary>
    QueuedProviderValidationFailed = 3,

    /// <summary>
    /// Deterministic checksum differs from the reference.
    /// </summary>
    ChecksumMismatch = 4,

    /// <summary>
    /// Topology or rebalance evidence differs from the reference.
    /// </summary>
    TopologyOrRebalanceMismatch = 5,

    /// <summary>
    /// Retained resource release had failures.
    /// </summary>
    RetainedResourceReleaseFailed = 6,

    /// <summary>
    /// Retained resource cleanup did not reach the required terminal state.
    /// </summary>
    RetainedResourceCleanupIncomplete = 7,

    /// <summary>
    /// Retained payload retention failed.
    /// </summary>
    RetainedResourceRetentionFailed = 8,

    /// <summary>
    /// Retained resource pressure telemetry is missing.
    /// </summary>
    MissingRetainedResourcePressureTelemetry = 9,

    /// <summary>
    /// Active retained resource telemetry is missing.
    /// </summary>
    MissingActiveRetainedTelemetry = 10,

    /// <summary>
    /// Combined retained payload budget was exceeded.
    /// </summary>
    CombinedRetainedPayloadBudgetExceeded = 11,

    /// <summary>
    /// Controlled evidence was excluded from the readiness decision.
    /// </summary>
    ControlledProofExcluded = 12,

    /// <summary>
    /// Candidate configuration did not match the intended contour.
    /// </summary>
    CandidateContourMismatch = 13,

    /// <summary>
    /// Candidate elapsed time regressed beyond the threshold.
    /// </summary>
    PerformanceRegression = 14,

    /// <summary>
    /// Candidate run spread exceeded the variance threshold.
    /// </summary>
    RunVarianceTooHigh = 15,

    /// <summary>
    /// Candidate allocation moved beyond the threshold.
    /// </summary>
    AllocationRegression = 16
}
