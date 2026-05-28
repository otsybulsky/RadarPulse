namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
{
    /// <summary>
    /// Synchronously disposes owned queue and async rebalance resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes owned queue and async rebalance resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        bool shouldDispose;
        lock (sync)
        {
            shouldDispose = !disposed;
            disposed = true;
        }

        if (!shouldDispose)
        {
            return;
        }

        if (ownsAsyncRebalanceSession && asyncRebalanceSession is not null)
        {
            await asyncRebalanceSession.DisposeAsync().ConfigureAwait(false);
        }

        if (ownsQueue)
        {
            queue.Dispose();
        }
    }

    private bool IsDisposed
    {
        get
        {
            lock (sync)
            {
                return disposed;
            }
        }
    }
}
