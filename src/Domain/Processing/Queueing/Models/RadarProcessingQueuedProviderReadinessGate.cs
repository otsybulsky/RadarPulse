namespace RadarPulse.Domain.Processing;

public enum RadarProcessingQueuedProviderReadinessGate
{
    CorrectnessParity = 1,
    TopologyAndRebalanceParity = 2,
    RetainedResourceReleaseHealth = 3,
    RetainedResourcePressure = 4,
    AllocationMovement = 5,
    PerformanceDelta = 6,
    RunVariance = 7,
    EffectiveConfiguration = 8,
    NaturalEvidence = 9
}
