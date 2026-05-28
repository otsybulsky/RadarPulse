using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    /// </summary>
    public RadarProcessingWorkerLifecycleResult Start()
    {
        lock (lifecycleSync)
        {
            var result = lifecycle.Start();
            if (!result.IsSuccess)
            {
                return result;
            }

            foreach (var worker in workers)
            {
                worker.Start(workerCancellation.Token, MarkFaulted);
            }

            return result;
        }
    }

    /// <summary>
    /// Stops accepting new dispatches while allowing workers to drain accepted requests.
    /// </summary>
    public RadarProcessingWorkerLifecycleResult StopAccepting()
    {
        RadarProcessingWorkerLifecycleResult result;
        lock (lifecycleSync)
        {
            result = lifecycle.StopAccepting();
        }

        if (result.IsSuccess)
        {
            CloseWorkers();
        }

        return result;
    }

    /// <summary>
    /// Stops accepting work and waits for worker loops to finish.
    /// </summary>
    public async ValueTask<RadarProcessingWorkerLifecycleResult> StopAsync()
    {
        RadarProcessingWorkerLifecycleResult result;
        lock (lifecycleSync)
        {
            result = lifecycle.Stop();
        }

        if (result.IsSuccess)
        {
            CloseWorkers();
            await AwaitWorkersAsync().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Dispatches a complete batch scope to worker mailboxes and waits for the batch barrier.
    /// </summary>
    /// <remarks>
    /// The method validates work item coverage before enqueue. When concurrent
    /// dispatch is not explicitly allowed, only one dispatch may be active at a
    /// time so mailbox capacity and completion accounting stay deterministic.
}
