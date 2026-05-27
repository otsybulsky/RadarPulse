using RadarPulse.Application.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Result of executing a production-pipeline run.
/// </summary>
public sealed class RadarProcessingProductionPipelineRunResult
{
    /// <summary>
    /// Creates a run result with configuration, readiness, read model, and runtime evidence.
    /// </summary>
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

    /// <summary>
    /// Stable run id.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Resolved configuration used for the run.
    /// </summary>
    public RadarProcessingProductionPipelineResolvedConfiguration Configuration { get; }

    /// <summary>
    /// Operator readiness summary for the run.
    /// </summary>
    public RadarProcessingProductionPipelineOperatorSummary OperatorSummary { get; }

    /// <summary>
    /// Store containing published BFF read models.
    /// </summary>
    public RadarProcessingBffReadModelStore ReadModelStore { get; }

    /// <summary>
    /// Runtime overlap result when processing executed.
    /// </summary>
    public RadarProcessingMvpRuntimeResult? RuntimeResult { get; }

    /// <summary>
    /// Published read model when processing produced one.
    /// </summary>
    public RadarProcessingRunReadModel? ReadModel { get; }

    /// <summary>
    /// Terminal run message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether a product read model is available.
    /// </summary>
    public bool HasReadModel => ReadModel is not null;

    /// <summary>
    /// Indicates whether the run completed and the overlap runtime reported completion.
    /// </summary>
    public bool IsCompleted =>
        OperatorSummary.RunState == RadarProcessingProductionPipelineRunState.Completed &&
        RuntimeResult?.OverlapResult.IsCompleted == true;
}
