namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Operator recommendation derived from production-pipeline readiness evidence.
/// </summary>
public enum RadarProcessingProductionPipelineFallbackRecommendation
{
    /// <summary>
    /// No fallback action is recommended.
    /// </summary>
    None = 1,

    /// <summary>
    /// Correct invalid configuration before running.
    /// </summary>
    FixConfiguration = 2,

    /// <summary>
    /// Inspect durable adapter compatibility or storage state.
    /// </summary>
    InspectDurableAdapter = 3,

    /// <summary>
    /// Recover an envelope left in claimed state.
    /// </summary>
    RecoverClaimedEnvelope = 4,

    /// <summary>
    /// Retry or poison a failed or abandoned envelope.
    /// </summary>
    RetryOrPoisonEnvelope = 5,

    /// <summary>
    /// Quarantine or otherwise handle a poison envelope.
    /// </summary>
    QuarantinePoisonEnvelope = 6,

    /// <summary>
    /// Cleanup canceled durable envelopes.
    /// </summary>
    CleanupCanceledEnvelope = 7,

    /// <summary>
    /// Release retained resources before considering the run ready.
    /// </summary>
    ReleaseRetainedResources = 8,

    /// <summary>
    /// Complete or recover uncommitted durable work.
    /// </summary>
    CompleteOrRecoverUncommittedWork = 9,

    /// <summary>
    /// Resolve handler output posture before using the run.
    /// </summary>
    ResolveHandlerPosture = 10,

    /// <summary>
    /// Reject a fallback that would silently change accepted semantics.
    /// </summary>
    RejectUnsafeFallback = 11
}
