using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineOperatorSummary
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

    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    public RadarProcessingProductionPipelineRunState RunState { get; }

    public RadarProcessingDurableRuntimeReadinessSummary DurableReadiness { get; }

    public RadarProcessingDurableAdapterSummary? DurableAdapter { get; }

    public RadarProcessingRetainedResourcePressureSummary RetainedPressure { get; }

    public bool ProcessingComplete { get; }

    public RadarProcessingProductionPipelineHandlerMode HandlerMode { get; }

    public bool HasHandlerConflict { get; }

    public string HandlerBlockingReason { get; }

    public bool IsReady { get; }

    public string FirstBlockingReason { get; }

    public bool HasBlockingReason => !string.IsNullOrWhiteSpace(FirstBlockingReason);

    public RadarProcessingProductionPipelineFallbackRecommendation FallbackRecommendation { get; }

    public RadarProcessingDurableBatchId? FirstBlockingBatchId =>
        DurableReadiness.FirstBlockingBatchId;

    public RadarProcessingQueuedBatchSequence? FirstBlockingSequence =>
        DurableReadiness.FirstBlockingSequence;

    public RadarProcessingDurableEnvelopeState? FirstBlockingState =>
        DurableReadiness.FirstBlockingState;

    public long CurrentRetainedBatchCount =>
        RetainedPressure.CurrentCombinedRetainedBatchCount;

    public long CurrentRetainedPayloadBytes =>
        RetainedPressure.CurrentCombinedRetainedPayloadBytes;

    public bool HasCurrentRetainedPressure =>
        CurrentRetainedBatchCount > 0 ||
        CurrentRetainedPayloadBytes > 0;

    public bool ReleaseHealthy =>
        !DurableReadiness.HasReleaseFailures &&
        !HasCurrentRetainedPressure;

    public bool HasWarnings => warnings.Count > 0;

    public IReadOnlyList<string> Warnings => warnings;

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

        if (hasHandlerConflict)
        {
            return new Blocker(
                string.IsNullOrWhiteSpace(handlerBlockingReason)
                    ? "handler output conflict"
                    : handlerBlockingReason,
                RadarProcessingProductionPipelineFallbackRecommendation.ResolveHandlerPosture);
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

    private readonly record struct Blocker(
        string Reason,
        RadarProcessingProductionPipelineFallbackRecommendation Recommendation)
    {
        public static Blocker None { get; } =
            new(string.Empty, RadarProcessingProductionPipelineFallbackRecommendation.None);
    }
}
