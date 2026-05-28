using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Synchronously disposes owned queue and async-core resources.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes owned queue and async-core resources.
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

        if (ownsAsyncCoreSession && asyncCoreSession is not null)
        {
            await asyncCoreSession.DisposeAsync().ConfigureAwait(false);
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
