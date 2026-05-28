namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableRebalanceSession
{
    /// <summary>
    /// Synchronously disposes pending completions and owned async rebalance resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes pending completions and owned async rebalance resources.
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

        DisposePendingCompletions();
        if (ownsAsyncRebalanceSession && asyncRebalanceSession is not null)
        {
            await asyncRebalanceSession.DisposeAsync().ConfigureAwait(false);
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

    private bool IsFaulted
    {
        get
        {
            lock (sync)
            {
                return faulted;
            }
        }
    }

    private bool IsCanceled
    {
        get
        {
            lock (sync)
            {
                return canceled;
            }
        }
    }
}
