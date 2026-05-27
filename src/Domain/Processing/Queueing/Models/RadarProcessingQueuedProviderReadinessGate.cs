namespace RadarPulse.Domain.Processing;

/// <summary>
/// Readiness gate categories used when promoting a queued provider contour.
/// </summary>
public enum RadarProcessingQueuedProviderReadinessGate
{
    /// <summary>
    /// Candidate output must match deterministic baseline processing output.
    /// </summary>
    CorrectnessParity = 1,

    /// <summary>
    /// Candidate topology and rebalance decisions must match the baseline surface.
    /// </summary>
    TopologyAndRebalanceParity = 2,

    /// <summary>
    /// Retained resources must release without failures.
    /// </summary>
    RetainedResourceReleaseHealth = 3,

    /// <summary>
    /// Retained resource pressure must remain within accepted limits.
    /// </summary>
    RetainedResourcePressure = 4,

    /// <summary>
    /// Allocation movement must stay within rollout thresholds.
    /// </summary>
    AllocationMovement = 5,

    /// <summary>
    /// Candidate elapsed-time behavior must not regress beyond rollout thresholds.
    /// </summary>
    PerformanceDelta = 6,

    /// <summary>
    /// Candidate run-to-run spread must stay within accepted variance.
    /// </summary>
    RunVariance = 7,

    /// <summary>
    /// Effective candidate configuration must match the intended contour.
    /// </summary>
    EffectiveConfiguration = 8,

    /// <summary>
    /// Natural workload evidence must support the controlled benchmark result.
    /// </summary>
    NaturalEvidence = 9
}
