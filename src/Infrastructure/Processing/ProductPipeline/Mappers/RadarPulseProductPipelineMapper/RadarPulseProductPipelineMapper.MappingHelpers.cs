using System.Globalization;
using RadarPulse.Application.Processing;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarPulseProductPipelineMapper
{
    private static RadarPulseProductConfigurationValue CreateValue<T>(
        string name,
        RadarProcessingProductionPipelineResolvedOption<T> option) =>
        new(
            name,
            FormatValue(option.Value),
            MapOptionSource(option.Source));

    private static string FormatValue<T>(T value) =>
        value switch
        {
            null => "none",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

    private static IReadOnlyList<string> CopyStrings(
        IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            copy[i] = values[i];
        }

        return Array.AsReadOnly(copy);
    }

    private static RadarPulseProductRunState MapRunState(
        RadarProcessingProductionPipelineRunState state) =>
        state switch
        {
            RadarProcessingProductionPipelineRunState.NotStarted => RadarPulseProductRunState.NotStarted,
            RadarProcessingProductionPipelineRunState.Running => RadarPulseProductRunState.Running,
            RadarProcessingProductionPipelineRunState.Draining => RadarPulseProductRunState.Draining,
            RadarProcessingProductionPipelineRunState.Completed => RadarPulseProductRunState.Completed,
            RadarProcessingProductionPipelineRunState.Stopped => RadarPulseProductRunState.Stopped,
            RadarProcessingProductionPipelineRunState.Blocked => RadarPulseProductRunState.Blocked,
            RadarProcessingProductionPipelineRunState.Failed => RadarPulseProductRunState.Failed,
            RadarProcessingProductionPipelineRunState.Canceled => RadarPulseProductRunState.Canceled,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

    private static RadarPulseProductHandlerMode MapHandlerMode(
        RadarProcessingProductionPipelineHandlerMode mode) =>
        mode switch
        {
            RadarProcessingProductionPipelineHandlerMode.Auto => RadarPulseProductHandlerMode.Auto,
            RadarProcessingProductionPipelineHandlerMode.HandlerFree => RadarPulseProductHandlerMode.HandlerFree,
            RadarProcessingProductionPipelineHandlerMode.MergeableDelta => RadarPulseProductHandlerMode.MergeableDelta,
            RadarProcessingProductionPipelineHandlerMode.SnapshotSequential => RadarPulseProductHandlerMode.SnapshotSequential,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    private static RadarPulseProductFallbackRecommendation MapFallbackRecommendation(
        RadarProcessingProductionPipelineFallbackRecommendation recommendation) =>
        recommendation switch
        {
            RadarProcessingProductionPipelineFallbackRecommendation.None => RadarPulseProductFallbackRecommendation.None,
            RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration => RadarPulseProductFallbackRecommendation.FixConfiguration,
            RadarProcessingProductionPipelineFallbackRecommendation.InspectDurableAdapter => RadarPulseProductFallbackRecommendation.InspectDurableAdapter,
            RadarProcessingProductionPipelineFallbackRecommendation.RecoverClaimedEnvelope => RadarPulseProductFallbackRecommendation.RecoverClaimedEnvelope,
            RadarProcessingProductionPipelineFallbackRecommendation.RetryOrPoisonEnvelope => RadarPulseProductFallbackRecommendation.RetryOrPoisonEnvelope,
            RadarProcessingProductionPipelineFallbackRecommendation.QuarantinePoisonEnvelope => RadarPulseProductFallbackRecommendation.QuarantinePoisonEnvelope,
            RadarProcessingProductionPipelineFallbackRecommendation.CleanupCanceledEnvelope => RadarPulseProductFallbackRecommendation.CleanupCanceledEnvelope,
            RadarProcessingProductionPipelineFallbackRecommendation.ReleaseRetainedResources => RadarPulseProductFallbackRecommendation.ReleaseRetainedResources,
            RadarProcessingProductionPipelineFallbackRecommendation.CompleteOrRecoverUncommittedWork => RadarPulseProductFallbackRecommendation.CompleteOrRecoverUncommittedWork,
            RadarProcessingProductionPipelineFallbackRecommendation.ResolveHandlerPosture => RadarPulseProductFallbackRecommendation.ResolveHandlerPosture,
            RadarProcessingProductionPipelineFallbackRecommendation.RejectUnsafeFallback => RadarPulseProductFallbackRecommendation.RejectUnsafeFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(recommendation))
        };

    private static RadarPulseProductOptionSource MapOptionSource(
        RadarProcessingProductionPipelineOptionSource source) =>
        source switch
        {
            RadarProcessingProductionPipelineOptionSource.Default => RadarPulseProductOptionSource.Default,
            RadarProcessingProductionPipelineOptionSource.Profile => RadarPulseProductOptionSource.Profile,
            RadarProcessingProductionPipelineOptionSource.ExplicitOverride => RadarPulseProductOptionSource.ExplicitOverride,
            RadarProcessingProductionPipelineOptionSource.TestHarness => RadarPulseProductOptionSource.TestHarness,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
}
