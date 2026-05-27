namespace RadarPulse.Domain.Processing;

/// <summary>
/// Status for one queued-provider rollout readiness gate.
/// </summary>
public enum RadarProcessingQueuedProviderReadinessStatus
{
    /// <summary>
    /// The gate was not evaluated for the current evidence set.
    /// </summary>
    NotEvaluated = 0,

    /// <summary>
    /// The gate passed with the supplied evidence.
    /// </summary>
    Passed = 1,

    /// <summary>
    /// The gate failed and blocks promotion.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The gate was evaluated but evidence was insufficient for a pass or fail.
    /// </summary>
    Inconclusive = 3
}
