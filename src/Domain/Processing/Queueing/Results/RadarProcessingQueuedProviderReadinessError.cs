namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedProviderReadinessError
{
    None = 0,
    NotEvaluated = 1,
    MissingBorrowedReference = 2,
    QueuedProviderValidationFailed = 3,
    ChecksumMismatch = 4,
    TopologyOrRebalanceMismatch = 5,
    RetainedResourceReleaseFailed = 6,
    RetainedResourceCleanupIncomplete = 7,
    RetainedResourceRetentionFailed = 8,
    MissingRetainedResourcePressureTelemetry = 9,
    MissingActiveRetainedTelemetry = 10,
    CombinedRetainedPayloadBudgetExceeded = 11,
    ControlledProofExcluded = 12,
    CandidateContourMismatch = 13,
    PerformanceRegression = 14,
    RunVarianceTooHigh = 15,
    AllocationRegression = 16
}
