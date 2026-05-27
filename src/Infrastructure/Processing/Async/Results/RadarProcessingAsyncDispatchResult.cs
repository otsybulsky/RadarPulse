using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingAsyncDispatchResult
{
    public RadarProcessingAsyncDispatchResult(
        RadarProcessingAsyncDispatchPlan plan,
        RadarProcessingAsyncWorkerGroupResult workerGroupResult)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(workerGroupResult);

        Plan = plan;
        WorkerGroupResult = workerGroupResult;
    }

    public RadarProcessingAsyncDispatchPlan Plan { get; }

    public RadarProcessingAsyncWorkerGroupResult WorkerGroupResult { get; }

    public RadarProcessingTopologyVersion TopologyVersion => Plan.TopologyVersion;

    public RadarProcessingBatchRoute Route => Plan.Route;

    public RadarProcessingAsyncBatchScopeResult? BatchResult => WorkerGroupResult.BatchResult;

    public RadarProcessingAsyncWorkerGroupDrainResult DrainResult => WorkerGroupResult.DrainResult;

    public bool IsSuccess => WorkerGroupResult.IsSuccess;
}
