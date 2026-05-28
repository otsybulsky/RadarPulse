using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Synchronously disposes pending completions and owned async core resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes pending completions and owned async core resources.
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

        foreach (var completion in pendingCompletions.Values)
        {
            completion.Dispose();
        }

        pendingCompletions.Clear();

        if (ownsAsyncCoreSession && asyncCoreSession is not null)
        {
            await asyncCoreSession.DisposeAsync().ConfigureAwait(false);
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
