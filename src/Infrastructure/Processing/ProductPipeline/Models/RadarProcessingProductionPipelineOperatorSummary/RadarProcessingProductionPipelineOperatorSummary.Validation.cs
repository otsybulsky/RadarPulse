using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingProductionPipelineOperatorSummary
{
    private static IReadOnlyList<string> MergeWarnings(
        IReadOnlyList<string> configurationWarnings,
        IReadOnlyList<string>? warnings)
    {
        if ((configurationWarnings.Count == 0) &&
            (warnings is null || warnings.Count == 0))
        {
            return Array.Empty<string>();
        }

        var result = new List<string>(configurationWarnings.Count + (warnings?.Count ?? 0));
        result.AddRange(configurationWarnings);
        if (warnings is not null)
        {
            result.AddRange(warnings);
        }

        return result;
    }

    private static IReadOnlyList<string> CopyWarnings(IReadOnlyList<string>? warnings)
    {
        if (warnings is null || warnings.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[warnings.Count];
        for (var i = 0; i < warnings.Count; i++)
        {
            copy[i] = warnings[i] ?? string.Empty;
        }

        return copy;
    }

    private static void EnsureKnownRunState(
        RadarProcessingProductionPipelineRunState runState)
    {
        if (runState is not RadarProcessingProductionPipelineRunState.NotStarted and
            not RadarProcessingProductionPipelineRunState.Running and
            not RadarProcessingProductionPipelineRunState.Draining and
            not RadarProcessingProductionPipelineRunState.Completed and
            not RadarProcessingProductionPipelineRunState.Stopped and
            not RadarProcessingProductionPipelineRunState.Blocked and
            not RadarProcessingProductionPipelineRunState.Failed and
            not RadarProcessingProductionPipelineRunState.Canceled)
        {
            throw new ArgumentOutOfRangeException(nameof(runState));
        }
    }

    private static void EnsureKnownHandlerMode(
        RadarProcessingProductionPipelineHandlerMode handlerMode)
    {
        if (handlerMode is not RadarProcessingProductionPipelineHandlerMode.Auto and
            not RadarProcessingProductionPipelineHandlerMode.HandlerFree and
            not RadarProcessingProductionPipelineHandlerMode.MergeableDelta and
            not RadarProcessingProductionPipelineHandlerMode.SnapshotSequential)
        {
            throw new ArgumentOutOfRangeException(nameof(handlerMode));
        }
    }

    private static void EnsureKnownFallbackRecommendation(
        RadarProcessingProductionPipelineFallbackRecommendation recommendation)
    {
        if (recommendation is not RadarProcessingProductionPipelineFallbackRecommendation.None and
            not RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration and
            not RadarProcessingProductionPipelineFallbackRecommendation.InspectDurableAdapter and
            not RadarProcessingProductionPipelineFallbackRecommendation.RecoverClaimedEnvelope and
            not RadarProcessingProductionPipelineFallbackRecommendation.RetryOrPoisonEnvelope and
            not RadarProcessingProductionPipelineFallbackRecommendation.QuarantinePoisonEnvelope and
            not RadarProcessingProductionPipelineFallbackRecommendation.CleanupCanceledEnvelope and
            not RadarProcessingProductionPipelineFallbackRecommendation.ReleaseRetainedResources and
            not RadarProcessingProductionPipelineFallbackRecommendation.CompleteOrRecoverUncommittedWork and
            not RadarProcessingProductionPipelineFallbackRecommendation.ResolveHandlerPosture and
            not RadarProcessingProductionPipelineFallbackRecommendation.RejectUnsafeFallback)
        {
            throw new ArgumentOutOfRangeException(nameof(recommendation));
        }
    }
}
