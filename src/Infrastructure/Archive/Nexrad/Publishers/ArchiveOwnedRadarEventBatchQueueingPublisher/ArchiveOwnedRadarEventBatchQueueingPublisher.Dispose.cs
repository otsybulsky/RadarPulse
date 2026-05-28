using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    /// <inheritdoc />
    public void Dispose()
    {
        bool shouldDispose;
        lock (sync)
        {
            shouldDispose = !disposed;
            disposed = true;
        }

        if (shouldDispose && ownsQueue)
        {
            queue.Dispose();
        }

        ReleasePendingResources();
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
