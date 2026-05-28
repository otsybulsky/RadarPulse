using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private RadarProcessingAsyncWorkerGroupDrainResult CaptureDrainResult(
        int acceptedWorkItemCount = 0,
        RadarProcessingAsyncBatchScopeResult? batchResult = null,
        TimeSpan barrierWaitTime = default,
        bool timedOut = false,
        bool cancellationRequested = false) =>
        new(
            acceptedWorkItemCount,
            batchResult?.Completion.RecordedWorkItemCount ?? 0,
            PendingWorkItemCount,
            RunningWorkItemCount,
            barrierWaitTime,
            timedOut,
            cancellationRequested);

    private static RadarProcessingAsyncBatchScopeResult CancelBeforeDispatch(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems)
    {
        foreach (var workItem in workItems)
        {
            scope.RecordCompletion(
                RadarProcessingAsyncWorkCompletion.Canceled(
                    workItem,
                    cancellationKind: RadarProcessingAsyncCancellationKind.BeforeDispatch));
        }

        return scope.Complete();
    }
}
