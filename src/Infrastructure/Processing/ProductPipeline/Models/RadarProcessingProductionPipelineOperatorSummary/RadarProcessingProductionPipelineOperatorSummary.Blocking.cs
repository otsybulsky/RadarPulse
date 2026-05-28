using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingProductionPipelineOperatorSummary
{
    private static Blocker ResolveBlocker(
        RadarProcessingProductionPipelineResolvedConfiguration configuration,
        RadarProcessingProductionPipelineRunState runState,
        RadarProcessingDurableRuntimeReadinessSummary durableReadiness,
        RadarProcessingDurableAdapterSummary? durableAdapter,
        RadarProcessingRetainedResourcePressureSummary retainedPressure,
        bool processingComplete,
        bool hasHandlerConflict,
        string handlerBlockingReason)
    {
        if (!configuration.IsValid)
        {
            return new Blocker(
                $"invalid configuration: {configuration.FirstInvalidOption}",
                RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration);
        }

        if (durableAdapter is not null && !durableAdapter.IsCompatible)
        {
            return new Blocker(
                durableAdapter.HasStorageMessage
                    ? durableAdapter.StorageMessage
                    : $"durable adapter {durableAdapter.CompatibilityStatus}",
                RadarProcessingProductionPipelineFallbackRecommendation.InspectDurableAdapter);
        }

        if (hasHandlerConflict)
        {
            return new Blocker(
                string.IsNullOrWhiteSpace(handlerBlockingReason)
                    ? "handler output conflict"
                    : handlerBlockingReason,
                RadarProcessingProductionPipelineFallbackRecommendation.ResolveHandlerPosture);
        }

        if (runState is RadarProcessingProductionPipelineRunState.Blocked or
            RadarProcessingProductionPipelineRunState.Failed)
        {
            return new Blocker(
                $"pipeline state {runState}",
                RadarProcessingProductionPipelineFallbackRecommendation.CompleteOrRecoverUncommittedWork);
        }

        if (runState == RadarProcessingProductionPipelineRunState.Canceled)
        {
            return new Blocker(
                "pipeline canceled",
                RadarProcessingProductionPipelineFallbackRecommendation.CleanupCanceledEnvelope);
        }

        if (!processingComplete)
        {
            return new Blocker(
                "processing completeness failed",
                RadarProcessingProductionPipelineFallbackRecommendation.CompleteOrRecoverUncommittedWork);
        }

        if (!durableReadiness.IsReady)
        {
            return new Blocker(
                durableReadiness.BlockingReason,
                MapDurableFallback(durableReadiness));
        }

        if (retainedPressure.CurrentCombinedRetainedBatchCount > 0 ||
            retainedPressure.CurrentCombinedRetainedPayloadBytes > 0)
        {
            return new Blocker(
                $"current retained pressure batches {retainedPressure.CurrentCombinedRetainedBatchCount}, bytes {retainedPressure.CurrentCombinedRetainedPayloadBytes}",
                RadarProcessingProductionPipelineFallbackRecommendation.ReleaseRetainedResources);
        }

        return Blocker.None;
    }

    private static RadarProcessingProductionPipelineFallbackRecommendation MapDurableFallback(
        RadarProcessingDurableRuntimeReadinessSummary readiness)
    {
        if (readiness.FirstBlockingState.HasValue)
        {
            return readiness.FirstBlockingState.Value switch
            {
                RadarProcessingDurableEnvelopeState.Claimed =>
                    RadarProcessingProductionPipelineFallbackRecommendation.RecoverClaimedEnvelope,
                RadarProcessingDurableEnvelopeState.Failed or
                    RadarProcessingDurableEnvelopeState.Abandoned =>
                    RadarProcessingProductionPipelineFallbackRecommendation.RetryOrPoisonEnvelope,
                RadarProcessingDurableEnvelopeState.Poison =>
                    RadarProcessingProductionPipelineFallbackRecommendation.QuarantinePoisonEnvelope,
                RadarProcessingDurableEnvelopeState.Canceled =>
                    RadarProcessingProductionPipelineFallbackRecommendation.CleanupCanceledEnvelope,
                _ => RadarProcessingProductionPipelineFallbackRecommendation.CompleteOrRecoverUncommittedWork
            };
        }

        if (readiness.HasReleaseFailures || readiness.HasTerminalRetainedPressure)
        {
            return RadarProcessingProductionPipelineFallbackRecommendation.ReleaseRetainedResources;
        }

        return RadarProcessingProductionPipelineFallbackRecommendation.CompleteOrRecoverUncommittedWork;
    }

    private readonly record struct Blocker(
        string Reason,
        RadarProcessingProductionPipelineFallbackRecommendation Recommendation)
    {
        public static Blocker None { get; } =
            new(string.Empty, RadarProcessingProductionPipelineFallbackRecommendation.None);
    }
}
