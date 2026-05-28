using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Operator readiness summary for production-pipeline configuration, durable state, and retention.
/// </summary>
/// <remarks>
/// The summary centralizes readiness and fallback recommendations so product
/// adapters and control commands use the same blocking reason vocabulary.
/// </remarks>
public sealed partial class RadarProcessingProductionPipelineOperatorSummary
{
    private readonly IReadOnlyList<string> warnings;

    private RadarProcessingProductionPipelineOperatorSummary(
        RadarProcessingProductionPipelineResolvedConfiguration configuration,
        RadarProcessingProductionPipelineRunState runState,
        RadarProcessingDurableRuntimeReadinessSummary durableReadiness,
        RadarProcessingDurableAdapterSummary? durableAdapter,
        RadarProcessingRetainedResourcePressureSummary retainedPressure,
        bool processingComplete,
        RadarProcessingProductionPipelineHandlerMode handlerMode,
        bool hasHandlerConflict,
        string handlerBlockingReason,
        bool isReady,
        string firstBlockingReason,
        RadarProcessingProductionPipelineFallbackRecommendation fallbackRecommendation,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        EnsureKnownRunState(runState);
        ArgumentNullException.ThrowIfNull(durableReadiness);
        ArgumentNullException.ThrowIfNull(retainedPressure);
        EnsureKnownHandlerMode(handlerMode);
        ArgumentNullException.ThrowIfNull(handlerBlockingReason);
        ArgumentNullException.ThrowIfNull(firstBlockingReason);
        EnsureKnownFallbackRecommendation(fallbackRecommendation);

        Configuration = configuration;
        RunState = runState;
        DurableReadiness = durableReadiness;
        DurableAdapter = durableAdapter;
        RetainedPressure = retainedPressure;
        ProcessingComplete = processingComplete;
        HandlerMode = handlerMode;
        HasHandlerConflict = hasHandlerConflict;
        HandlerBlockingReason = handlerBlockingReason;
        IsReady = isReady;
        FirstBlockingReason = firstBlockingReason;
        FallbackRecommendation = fallbackRecommendation;
        this.warnings = CopyWarnings(warnings);
    }

    /// <summary>
    /// Resolved production-pipeline configuration.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    /// <summary>
    /// Current run lifecycle state.
    /// </summary>
    public RadarProcessingProductionPipelineRunState RunState { get; }

    /// <summary>
    /// Durable runtime readiness derived from queue state.
    /// </summary>
    public RadarProcessingDurableRuntimeReadinessSummary DurableReadiness { get; }

    /// <summary>
    /// Durable adapter evidence when a persistent adapter is involved.
    /// </summary>
    public RadarProcessingDurableAdapterSummary? DurableAdapter { get; }

    /// <summary>
    /// Retained resource pressure evidence.
    /// </summary>
    public RadarProcessingRetainedResourcePressureSummary RetainedPressure { get; }

    /// <summary>
    /// Indicates whether processing completeness checks passed.
    /// </summary>
    public bool ProcessingComplete { get; }

    /// <summary>
    /// Handler output mode used by the run.
    /// </summary>
    public RadarProcessingProductionPipelineHandlerMode HandlerMode { get; }

    /// <summary>
    /// Indicates whether handler posture blocked the run.
    /// </summary>
    public bool HasHandlerConflict { get; }

    /// <summary>
    /// Handler blocking reason when handler posture blocks readiness.
    /// </summary>
    public string HandlerBlockingReason { get; }

    /// <summary>
    /// Indicates whether the run is ready for product-facing consumption.
    /// </summary>
    public bool IsReady { get; }

    /// <summary>
    /// First blocking reason preventing readiness.
    /// </summary>
    public string FirstBlockingReason { get; }

    /// <summary>
    /// Indicates whether a blocking reason is present.
    /// </summary>
    public bool HasBlockingReason => !string.IsNullOrWhiteSpace(FirstBlockingReason);

    /// <summary>
    /// Recommended fallback action derived from the first blocker.
    /// </summary>
    public RadarProcessingProductionPipelineFallbackRecommendation FallbackRecommendation { get; }

    /// <summary>
    /// First blocking durable batch id when readiness is blocked by durable state.
    /// </summary>
    public RadarProcessingDurableBatchId? FirstBlockingBatchId =>
        DurableReadiness.FirstBlockingBatchId;

    /// <summary>
    /// First blocking provider sequence when readiness is blocked by durable state.
    /// </summary>
    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence =>
        DurableReadiness.FirstBlockingSequence;

    /// <summary>
    /// First blocking durable envelope state.
    /// </summary>
    public RadarProcessingDurableEnvelopeState? FirstBlockingState =>
        DurableReadiness.FirstBlockingState;

    /// <summary>
    /// Current retained batch count across retained-resource pressure evidence.
    /// </summary>
    public long CurrentRetainedBatchCount =>
        RetainedPressure.CurrentCombinedRetainedBatchCount;

    /// <summary>
    /// Current retained payload bytes across retained-resource pressure evidence.
    /// </summary>
    public long CurrentRetainedPayloadBytes =>
        RetainedPressure.CurrentCombinedRetainedPayloadBytes;

    /// <summary>
    /// Indicates whether current retained resources are still outstanding.
    /// </summary>
    public bool HasCurrentRetainedPressure =>
        CurrentRetainedBatchCount > 0 ||
        CurrentRetainedPayloadBytes > 0;

    /// <summary>
    /// Indicates whether release checks found no failures or current retained pressure.
    /// </summary>
    public bool ReleaseHealthy =>
        !DurableReadiness.HasReleaseFailures &&
        !HasCurrentRetainedPressure;

    /// <summary>
    /// Indicates whether warnings are present.
    /// </summary>
    public bool HasWarnings => warnings.Count > 0;

    /// <summary>
    /// Warning messages attached to the run summary.
    /// </summary>
    public IReadOnlyList<string> Warnings => warnings;
}
