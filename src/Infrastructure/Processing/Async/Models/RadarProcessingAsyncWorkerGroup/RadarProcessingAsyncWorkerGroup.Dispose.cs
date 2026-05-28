using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Synchronously disposes the worker group.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes the worker group and ignores the lifecycle result.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeWithResultAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes workers and returns the lifecycle transition result.
    /// </summary>
    public async ValueTask<RadarProcessingWorkerLifecycleResult> DisposeWithResultAsync()
    {
        RadarProcessingWorkerLifecycleResult result;
        lock (lifecycleSync)
        {
            result = lifecycle.Dispose();
        }

        if (Interlocked.Exchange(ref disposeRequested, 1) == 0)
        {
            CloseWorkers();
        }

        await AwaitWorkersAsync().ConfigureAwait(false);
        if (Volatile.Read(ref disposeRequested) != 0)
        {
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }

        if (Interlocked.Exchange(ref cancellationDisposed, 1) == 0)
        {
            workerCancellation.Dispose();
        }

        return result;
    }
}
