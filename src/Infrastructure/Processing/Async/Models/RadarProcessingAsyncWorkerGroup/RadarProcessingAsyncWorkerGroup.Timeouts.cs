using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private async ValueTask<(
        RadarProcessingAsyncTimeoutResult TimeoutResult,
        RadarProcessingWorkerGroupHealthTransition? HealthTransition)> WaitForTimeoutOrCompletionAsync(
        Task<RadarProcessingAsyncBatchScopeResult> completion,
        CancellationTokenSource workCancellation,
        RadarProcessingAsyncWorkerGroupBatchState batchState)
    {
        if (!Options.Execution.HasBatchTimeout)
        {
            return (RadarProcessingAsyncTimeoutResult.None, null);
        }

        var timeout = Options.Execution.BatchTimeout!.Value;
        var completed = await Task.WhenAny(
            completion,
            Task.Delay(timeout)).ConfigureAwait(false);
        if (ReferenceEquals(completed, completion))
        {
            return (RadarProcessingAsyncTimeoutResult.None, null);
        }

        var cancellationRequested = Options.Execution.TimeoutPolicy ==
            RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy;
        var healthTransition = MarkFaulted(RadarProcessingAsyncFailureKind.TimedOut);
        if (Options.Execution.TimeoutPolicy ==
            RadarProcessingWorkerTimeoutPolicy.RequestCancellationAndMarkUnhealthy)
        {
            batchState.MarkTimeoutCancellationRequested();
            await workCancellation.CancelAsync().ConfigureAwait(false);
        }

        return (
            new RadarProcessingAsyncTimeoutResult(
                timedOut: true,
                timeout: timeout,
                timeoutPolicy: Options.Execution.TimeoutPolicy,
                cancellationRequested: cancellationRequested),
            healthTransition);
    }
}
