namespace RadarPulse.Domain.Processing;

/// <summary>
/// Error classification for queued-provider semantic validation.
/// </summary>
public enum RadarProcessingQueuedProviderValidationError
{
    /// <summary>
    /// No validation error.
    /// </summary>
    None = 0,

    /// <summary>
    /// A queued batch did not own its payload.
    /// </summary>
    NonOwnedQueuedBatch = 1,

    /// <summary>
    /// Provider sequence moved backwards.
    /// </summary>
    ProviderSequenceRegression = 2,

    /// <summary>
    /// Processing sequence moved backwards.
    /// </summary>
    ProcessingSequenceRegression = 3,

    /// <summary>
    /// An accepted batch did not have a completion result.
    /// </summary>
    MissingCompletionForAcceptedBatch = 4,

    /// <summary>
    /// Topology version regressed between results.
    /// </summary>
    TopologyVersionRegression = 5,

    /// <summary>
    /// Queue telemetry counters did not match result evidence.
    /// </summary>
    TelemetryCounterMismatch = 6,

    /// <summary>
    /// Queue fault status did not match result evidence.
    /// </summary>
    QueueFaultStateMismatch = 7,

    /// <summary>
    /// Worker failure count did not match reference evidence.
    /// </summary>
    WorkerFailureCountMismatch = 8,

    /// <summary>
    /// Deterministic checksum did not match reference evidence.
    /// </summary>
    DeterministicChecksumMismatch = 9,

    /// <summary>
    /// Accepted rebalance move count did not match reference evidence.
    /// </summary>
    AcceptedMoveCountMismatch = 10,

    /// <summary>
    /// Skipped rebalance decision count did not match reference evidence.
    /// </summary>
    SkippedDecisionCountMismatch = 11,

    /// <summary>
    /// Failed batch count did not match reference evidence.
    /// </summary>
    FailureCountMismatch = 12,

    /// <summary>
    /// Final topology version did not match reference evidence.
    /// </summary>
    FinalTopologyVersionMismatch = 13,

    /// <summary>
    /// Provider sequence had a gap.
    /// </summary>
    ProviderSequenceGap = 14,

    /// <summary>
    /// Processing sequence had a gap.
    /// </summary>
    ProcessingSequenceGap = 15,

    /// <summary>
    /// Payload value count did not match reference evidence.
    /// </summary>
    PayloadValueCountMismatch = 16,

    /// <summary>
    /// Failed migration count did not match reference evidence.
    /// </summary>
    FailedMigrationCountMismatch = 17,

    /// <summary>
    /// Reference semantic surface did not match the validation context.
    /// </summary>
    ReferenceSemanticSurfaceMismatch = 18,

    /// <summary>
    /// Retention telemetry was missing required evidence.
    /// </summary>
    RetentionTelemetryIncomplete = 19,

    /// <summary>
    /// Retention telemetry did not match expected strategy or counts.
    /// </summary>
    RetentionTelemetryMismatch = 20,

    /// <summary>
    /// Retained resources were not fully cleaned up.
    /// </summary>
    RetainedResourceCleanupIncomplete = 21,

    /// <summary>
    /// Overlap telemetry was missing required evidence.
    /// </summary>
    OverlapTelemetryIncomplete = 22
}
