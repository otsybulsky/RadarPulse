using RadarPulse.Application.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingProductionPipelineRunResult
{
    public RadarProcessingProductionPipelineRunResult(
        string runId,
        RadarProcessingProductionPipelineResolvedConfiguration configuration,
        RadarProcessingProductionPipelineOperatorSummary operatorSummary,
        RadarProcessingBffReadModelStore readModelStore,
        RadarProcessingMvpRuntimeResult? runtimeResult = null,
        RadarProcessingRunReadModel? readModel = null,
        string message = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(operatorSummary);
        ArgumentNullException.ThrowIfNull(readModelStore);
        ArgumentNullException.ThrowIfNull(message);

        RunId = runId;
        Configuration = configuration;
        OperatorSummary = operatorSummary;
        ReadModelStore = readModelStore;
        RuntimeResult = runtimeResult;
        ReadModel = readModel;
        Message = message;
    }

    public string RunId { get; }

    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    public RadarProcessingProductionPipelineOperatorSummary OperatorSummary { get; }

    public RadarProcessingBffReadModelStore ReadModelStore { get; }

    public RadarProcessingMvpRuntimeResult? RuntimeResult { get; }

    public RadarProcessingRunReadModel? ReadModel { get; }

    public string Message { get; }

    public bool HasReadModel => ReadModel is not null;

    public bool IsCompleted =>
        OperatorSummary.RunState == RadarProcessingProductionPipelineRunState.Completed &&
        RuntimeResult?.OverlapResult.IsCompleted == true;
}
