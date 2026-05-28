using RadarPulse.Application.Processing;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Maps infrastructure production-pipeline evidence into product API/read-model shapes.
/// </summary>
public static partial class RadarPulseProductPipelineMapper
{
    /// <summary>
    /// Maps a full run result into product run detail.
    /// </summary>
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
}
