using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingWorkerTelemetryRecorderTests
{
    private static RadarProcessingAsyncDispatchResult CreateDispatch(
        RadarProcessingAsyncDispatchPlan plan,
        RadarProcessingAsyncWorkerGroupResult workerResult) =>
        new(plan, workerResult);

    private static RadarProcessingAsyncDispatchResult CreateDispatch(
        (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult) pair) =>
        new(pair.Plan, pair.WorkerResult);

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateSucceededResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = CreateSucceededCompletions(plan);
        var batchResult = CreateBatchResult(plan, completions);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Completed(
                CreateHealthyStatus(plan),
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    acceptedWorkItemCount: completions.Length,
                    completedWorkItemCount: completions.Length,
                    barrierWaitTime: TimeSpan.FromMilliseconds(4))));
    }

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateFailedResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = new[]
        {
            RadarProcessingAsyncWorkCompletion.Failed(
                plan.WorkItems[0],
                failureKind: RadarProcessingAsyncFailureKind.WorkerException),
            CreateSucceededCompletion(plan, workItemId: 1)
        };
        var batchResult = CreateBatchResult(
            plan,
            completions,
            RadarProcessingAsyncBatchCompletionError.WorkFailed);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Completed(
                CreateHealthyStatus(plan),
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    acceptedWorkItemCount: completions.Length,
                    completedWorkItemCount: completions.Length)));
    }

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateCanceledResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = plan.WorkItems
            .Select(static workItem =>
                RadarProcessingAsyncWorkCompletion.Canceled(
                    workItem,
                    cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch))
            .ToArray();
        var batchResult = CreateBatchResult(
            plan,
            completions,
            RadarProcessingAsyncBatchCompletionError.WorkCanceled);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Completed(
                CreateHealthyStatus(plan),
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    completedWorkItemCount: completions.Length,
                    cancellationRequested: true),
                cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch));
    }

    private static (RadarProcessingAsyncDispatchPlan Plan, RadarProcessingAsyncWorkerGroupResult WorkerResult)
        CreateTimedOutResult(RadarProcessingAsyncDispatchPlan plan)
    {
        var completions = CreateSucceededCompletions(plan);
        var batchResult = CreateBatchResult(plan, completions);
        var previous = CreateHealthyStatus(plan);
        var current = new RadarProcessingWorkerGroupStatus(
            RadarProcessingWorkerGroupState.Faulted,
            RadarProcessingWorkerHealth.Faulted,
            plan.ShardCount,
            queueCapacity: 1,
            RadarProcessingWorkerLifecycleError.Faulted);
        return (
            plan,
            RadarProcessingAsyncWorkerGroupResult.Rejected(
                current,
                RadarProcessingAsyncWorkerGroupError.TimedOut,
                batchResult,
                new RadarProcessingAsyncWorkerGroupDrainResult(
                    acceptedWorkItemCount: completions.Length,
                    completedWorkItemCount: completions.Length,
                    barrierWaitTime: TimeSpan.FromMilliseconds(7),
                    timedOut: true),
                RadarProcessingAsyncFailureKind.TimedOut,
                timeoutResult: new RadarProcessingAsyncTimeoutResult(
                    timedOut: true,
                    timeout: TimeSpan.FromMilliseconds(5),
                    timeoutPolicy: RadarProcessingWorkerTimeoutPolicy.MarkUnhealthy),
                healthTransition: new RadarProcessingWorkerGroupHealthTransition(
                    previous,
                    current,
                    RadarProcessingAsyncFailureKind.TimedOut)));
    }
}
