using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Combined dispatch plan and worker group outcome for one async batch.
/// </summary>
public sealed record RadarProcessingAsyncDispatchResult
{
    /// <summary>
    /// Creates a dispatch result from the immutable plan and worker group result.
    /// </summary>
    public RadarProcessingAsyncDispatchResult(
        RadarProcessingAsyncDispatchPlan plan,
        RadarProcessingAsyncWorkerGroupResult workerGroupResult)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(workerGroupResult);

        Plan = plan;
        WorkerGroupResult = workerGroupResult;
    }

    /// <summary>
    /// Plan used for the dispatch.
    /// </summary>
    public RadarProcessingAsyncDispatchPlan Plan { get; }

    /// <summary>
    /// Worker group outcome for the dispatch.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupResult WorkerGroupResult { get; }

    /// <summary>
    /// Topology version captured by the dispatch plan.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion => Plan.TopologyVersion;

    /// <summary>
    /// Batch route captured by the dispatch plan.
    /// </summary>
    public RadarProcessingBatchRoute Route => Plan.Route;

    /// <summary>
    /// Batch scope result produced by the worker group when available.
    /// </summary>
    public RadarProcessingAsyncBatchScopeResult? BatchResult => WorkerGroupResult.BatchResult;

    /// <summary>
    /// Drain evidence captured after dispatch completion or rejection.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupDrainResult DrainResult => WorkerGroupResult.DrainResult;

    /// <summary>
    /// Indicates whether the worker group completed the batch successfully.
    /// </summary>
    public bool IsSuccess => WorkerGroupResult.IsSuccess;
}
