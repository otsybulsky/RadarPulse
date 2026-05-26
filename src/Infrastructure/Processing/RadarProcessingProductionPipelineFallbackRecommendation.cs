namespace RadarPulse.Infrastructure.Processing;

public enum RadarProcessingProductionPipelineFallbackRecommendation
{
    None = 1,
    FixConfiguration = 2,
    InspectDurableAdapter = 3,
    RecoverClaimedEnvelope = 4,
    RetryOrPoisonEnvelope = 5,
    QuarantinePoisonEnvelope = 6,
    CleanupCanceledEnvelope = 7,
    ReleaseRetainedResources = 8,
    CompleteOrRecoverUncommittedWork = 9,
    ResolveHandlerPosture = 10,
    RejectUnsafeFallback = 11
}
