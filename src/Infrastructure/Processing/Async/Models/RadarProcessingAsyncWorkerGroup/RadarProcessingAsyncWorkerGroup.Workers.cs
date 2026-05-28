using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private static RadarProcessingAsyncWorker[] CreateWorkers(
        RadarProcessingAsyncWorkerGroupOptions options)
    {
        var workers = new RadarProcessingAsyncWorker[options.WorkerCount];
        var mailboxOptions = new RadarProcessingWorkerMailboxOptions(options.QueueCapacity);
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new RadarProcessingAsyncWorker(new RadarProcessingWorkerId(i), mailboxOptions);
        }

        return workers;
    }

    private static RadarProcessingAsyncWorkerGroupError MapLifecycleError(
        RadarProcessingWorkerLifecycleError error) =>
        error switch
        {
            RadarProcessingWorkerLifecycleError.None => RadarProcessingAsyncWorkerGroupError.None,
            RadarProcessingWorkerLifecycleError.AlreadyStarted => RadarProcessingAsyncWorkerGroupError.AlreadyStarted,
            RadarProcessingWorkerLifecycleError.NotStarted => RadarProcessingAsyncWorkerGroupError.NotStarted,
            RadarProcessingWorkerLifecycleError.NotRunning => RadarProcessingAsyncWorkerGroupError.NotRunning,
            RadarProcessingWorkerLifecycleError.Stopping => RadarProcessingAsyncWorkerGroupError.Stopping,
            RadarProcessingWorkerLifecycleError.Stopped => RadarProcessingAsyncWorkerGroupError.Stopped,
            RadarProcessingWorkerLifecycleError.Faulted => RadarProcessingAsyncWorkerGroupError.Faulted,
            RadarProcessingWorkerLifecycleError.Disposed => RadarProcessingAsyncWorkerGroupError.Disposed,
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };

    private void CloseWorkers()
    {
        foreach (var worker in workers)
        {
            worker.Close();
        }
    }

    private async ValueTask AwaitWorkersAsync()
    {
        if (workers.Length == 0)
        {
            return;
        }

        await Task.WhenAll(workers.Select(static worker => worker.Completion)).ConfigureAwait(false);
    }

    private void MarkFaulted(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        MarkFaulted(RadarProcessingAsyncFailureKind.WorkerGroupFaulted);
    }

    private RadarProcessingWorkerGroupHealthTransition MarkFaulted(
        RadarProcessingAsyncFailureKind failureKind)
    {
        lock (lifecycleSync)
        {
            var previous = lifecycle.Status;
            var result = lifecycle.MarkFaulted();
            return new RadarProcessingWorkerGroupHealthTransition(
                previous,
                result.Status,
                failureKind);
        }
    }
}
