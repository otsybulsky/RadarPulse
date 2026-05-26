using System.Globalization;
using RadarPulse.Application.Processing;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Processing;

public static class RadarPulseProductPipelineMapper
{
    public static RadarPulseProductRunDetail ToProductRunDetail(
        RadarProcessingProductionPipelineRunResult result,
        RadarPulseProductInputSummary input,
        RadarProcessingProductionPipelineCapacityEvidence? capacityEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(input);

        capacityEvidence ??= RadarProcessingProductionPipelineCapacityEvidence.FromRunResult(result);
        var configuration = ToProductConfiguration(result.Configuration);
        var operatorSummary = ToProductOperatorSummary(result.OperatorSummary);
        var capacity = ToProductCapacityEvidence(capacityEvidence);
        var diagnostics = result.ReadModel is null
            ? null
            : ToProductDiagnostics(result.ReadModel.Diagnostics);
        var handlerContract = result.ReadModel is null
            ? null
            : ToProductHandlerContract(result.ReadModel.HandlerOutputContract);
        var batches = result.ReadModel is null
            ? Array.Empty<RadarPulseProductBatch>()
            : result.ReadModel.Batches.Select(ToProductBatch).ToArray();
        var sources = result.ReadModel is null
            ? Array.Empty<RadarPulseProductSource>()
            : result.ReadModel.Sources.Select(ToProductSource).ToArray();

        var summary = new RadarPulseProductRunSummary(
            result.RunId,
            input,
            MapRunState(result.OperatorSummary.RunState),
            result.OperatorSummary.IsReady,
            result.HasReadModel,
            MapHandlerMode(result.OperatorSummary.HandlerMode),
            result.OperatorSummary.FirstBlockingReason,
            MapFallbackRecommendation(result.OperatorSummary.FallbackRecommendation),
            batches.Length,
            sources.Length,
            capacityEvidence.AcceptedBatchCount,
            capacityEvidence.ProcessedBatchCount,
            capacityEvidence.CommittedBatchCount,
            operatorSummary.Warnings.Count);

        return new RadarPulseProductRunDetail(
            summary,
            configuration,
            operatorSummary,
            capacity,
            diagnostics,
            handlerContract,
            Array.AsReadOnly(batches),
            Array.AsReadOnly(sources),
            result.Message);
    }

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

    public static RadarPulseProductBatch ToProductBatch(
        RadarProcessingBatchReadModel batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new RadarPulseProductBatch(
            batch.ProviderSequence,
            batch.WasAccepted,
            batch.StreamEventCount,
            batch.PayloadBytes,
            batch.PayloadValueCount,
            batch.RawValueChecksum,
            batch.ProcessingStatus?.ToString(),
            batch.IsSuccessful,
            batch.Message,
            batch.TopologyVersion?.Value);
    }

    public static RadarPulseProductSource ToProductSource(
        RadarProcessingSourceOutputReadModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var handlerValues = source.HandlerValues
            .Select(ToProductHandlerOutput)
            .ToArray();

        return new RadarPulseProductSource(
            new RadarPulseProductSourceIdentity(
                source.Identity.SourceId,
                source.Identity.RadarOrdinal,
                source.Identity.ElevationSlot,
                source.Identity.AzimuthBucket,
                source.Identity.RangeBand),
            source.IsActive,
            source.ProcessedEventCount,
            source.ProcessedPayloadValueCount,
            source.RawValueChecksum,
            source.LastMessageTimestampUtcTicks,
            source.ProcessingChecksum,
            Array.AsReadOnly(handlerValues));
    }

    public static RadarPulseProductHandlerContract ToProductHandlerContract(
        RadarProcessingHandlerOutputContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var handlers = contract.Handlers
            .Select(ToProductHandlerDescriptor)
            .ToArray();

        return new RadarPulseProductHandlerContract(
            contract.StatePosture.ToString(),
            contract.Message,
            contract.FirstBlockingReason,
            contract.IsBlocked,
            Array.AsReadOnly(handlers));
    }

    public static RadarPulseProductHandlerOutput ToProductHandlerOutput(
        RadarProcessingHandlerOutputValueReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new RadarPulseProductHandlerOutput(
            value.HandlerIndex,
            value.HandlerName,
            value.Name,
            value.Type.ToString(),
            value.Int64Value,
            value.DoubleValue);
    }

    private static RadarPulseProductHandlerDescriptor ToProductHandlerDescriptor(
        RadarProcessingHandlerOutputDescriptor descriptor)
    {
        var fields = descriptor.Fields
            .Select(static field => new RadarPulseProductHandlerField(
                field.HandlerIndex,
                field.HandlerName,
                field.Name,
                field.Type.ToString(),
                field.SlotIndex))
            .ToArray();

        return new RadarPulseProductHandlerDescriptor(
            descriptor.HandlerIndex,
            descriptor.Name,
            descriptor.Int64SlotCount,
            descriptor.DoubleSlotCount,
            descriptor.ExecutionClassification.ToString(),
            Array.AsReadOnly(fields));
    }

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
