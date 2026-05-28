using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingProductionPipelineOperatorSummary
{
    /// <summary>
    /// Creates an operator summary and resolves the first blocker.
    /// </summary>
    public static RadarProcessingProductionPipelineOperatorSummary Create(
        RadarProcessingProductionPipelineResolvedConfiguration configuration,
        RadarProcessingProductionPipelineRunState runState =
            RadarProcessingProductionPipelineRunState.Completed,
        RadarProcessingDurableRuntimeReadinessSummary? durableReadiness = null,
        RadarProcessingDurableAdapterSummary? durableAdapter = null,
        RadarProcessingRetainedResourcePressureSummary? retainedPressure = null,
        bool processingComplete = true,
        RadarProcessingProductionPipelineHandlerMode? handlerMode = null,
        bool hasHandlerConflict = false,
        string handlerBlockingReason = "",
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        durableReadiness ??= durableAdapter is null
            ? RadarProcessingDurableRuntimeReadinessSummary.Empty
            : new RadarProcessingDurableRuntimeReadinessSummary(durableAdapter.QueueSummary);
        retainedPressure ??= RadarProcessingRetainedResourcePressureSummary.Empty;
        var effectiveHandlerMode = handlerMode ?? configuration.HandlerMode.Value;
        EnsureKnownRunState(runState);
        EnsureKnownHandlerMode(effectiveHandlerMode);

        var allWarnings = MergeWarnings(configuration.Warnings, warnings);
        var blocker = ResolveBlocker(
            configuration,
            runState,
            durableReadiness,
            durableAdapter,
            retainedPressure,
            processingComplete,
            hasHandlerConflict,
            handlerBlockingReason);

        return new RadarProcessingProductionPipelineOperatorSummary(
            configuration,
            runState,
            durableReadiness,
            durableAdapter,
            retainedPressure,
            processingComplete,
            effectiveHandlerMode,
            hasHandlerConflict,
            handlerBlockingReason,
            blocker.Recommendation == RadarProcessingProductionPipelineFallbackRecommendation.None,
            blocker.Reason,
            blocker.Recommendation,
            allWarnings);
    }
}
