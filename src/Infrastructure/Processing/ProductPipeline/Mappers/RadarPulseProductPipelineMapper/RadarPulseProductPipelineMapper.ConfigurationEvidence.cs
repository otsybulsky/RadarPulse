using RadarPulse.Application.Processing;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarPulseProductPipelineMapper
{
    /// <summary>
    /// Maps resolved infrastructure configuration into product configuration vocabulary.
    /// </summary>
    public static RadarPulseProductConfiguration ToProductConfiguration(
        RadarProcessingProductionPipelineResolvedConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var values = new[]
        {
            CreateValue(nameof(configuration.ProviderMode), configuration.ProviderMode),
            CreateValue(nameof(configuration.ProviderOverlapMode), configuration.ProviderOverlapMode),
            CreateValue(nameof(configuration.RetentionStrategy), configuration.RetentionStrategy),
            CreateValue(nameof(configuration.ExecutionMode), configuration.ExecutionMode),
            CreateValue(nameof(configuration.WorkerCount), configuration.WorkerCount),
            CreateValue(nameof(configuration.WorkerQueueCapacity), configuration.WorkerQueueCapacity),
            CreateValue(nameof(configuration.ProviderQueueCapacity), configuration.ProviderQueueCapacity),
            CreateValue(nameof(configuration.RetainedPayloadBytes), configuration.RetainedPayloadBytes),
            CreateValue(nameof(configuration.OrderedActiveBatchCapacity), configuration.OrderedActiveBatchCapacity),
            CreateValue(nameof(configuration.DurableAdapterKind), configuration.DurableAdapterKind),
            CreateValue(nameof(configuration.HandlerMode), configuration.HandlerMode),
            CreateValue(nameof(configuration.WorkloadBatchLimit), configuration.WorkloadBatchLimit)
        };

        return new RadarPulseProductConfiguration(
            configuration.ProfileName,
            configuration.IsValid,
            configuration.FirstInvalidOption,
            configuration.FirstInvalidReason,
            Array.AsReadOnly(values),
            CopyStrings(configuration.Warnings));
    }

    /// <summary>
    /// Maps operator readiness evidence into product vocabulary.
    /// </summary>
    public static RadarPulseProductOperatorSummary ToProductOperatorSummary(
        RadarProcessingProductionPipelineOperatorSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new RadarPulseProductOperatorSummary(
            MapRunState(summary.RunState),
            summary.IsReady,
            summary.ProcessingComplete,
            MapHandlerMode(summary.HandlerMode),
            summary.HasHandlerConflict,
            summary.HandlerBlockingReason,
            summary.FirstBlockingReason,
            MapFallbackRecommendation(summary.FallbackRecommendation),
            summary.FirstBlockingBatchId?.Value,
            summary.FirstBlockingSequence?.Value,
            summary.FirstBlockingState?.ToString(),
            summary.CurrentRetainedBatchCount,
            summary.CurrentRetainedPayloadBytes,
            summary.ReleaseHealthy,
            CopyStrings(summary.Warnings));
    }

    /// <summary>
    /// Maps capacity evidence into product vocabulary.
    /// </summary>
    public static RadarPulseProductCapacityEvidence ToProductCapacityEvidence(
        RadarProcessingProductionPipelineCapacityEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return new RadarPulseProductCapacityEvidence(
            evidence.RunId,
            evidence.ProfileName,
            evidence.Elapsed.TotalMilliseconds,
            evidence.MeasuredAllocatedBytes,
            evidence.AcceptedBatchCount,
            evidence.ProcessedBatchCount,
            evidence.CommittedBatchCount,
            MapHandlerMode(evidence.HandlerMode),
            evidence.DurableAdapterKind.ToString(),
            evidence.TerminalRetainedBatchCount,
            evidence.TerminalRetainedPayloadBytes,
            evidence.ProcessingCompletenessPassed,
            evidence.IsReady,
            evidence.FirstBlockingReason,
            evidence.ConfigurationContour);
    }

    /// <summary>
    /// Maps BFF diagnostics into product diagnostics vocabulary.
    /// </summary>
    public static RadarPulseProductDiagnostics ToProductDiagnostics(
        RadarProcessingRunDiagnosticsReadModel diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new RadarPulseProductDiagnostics(
            diagnostics.ProcessingCompletenessPassed,
            diagnostics.IsReady,
            diagnostics.BlockingReason,
            diagnostics.HandlerOutputProvenance.ToString(),
            diagnostics.UsesOrderedHandlerDeltaMerge,
            diagnostics.UsesSequentialHandlerFallback,
            diagnostics.HandlerOutputBlocked,
            diagnostics.ReleaseFailureCount,
            diagnostics.TerminalRetainedEnvelopeCount,
            diagnostics.TerminalRetainedPayloadBytes,
            diagnostics.CurrentCombinedRetainedBatchCount,
            diagnostics.CurrentCombinedRetainedPayloadBytes,
            CopyStrings(diagnostics.Warnings));
    }
}
