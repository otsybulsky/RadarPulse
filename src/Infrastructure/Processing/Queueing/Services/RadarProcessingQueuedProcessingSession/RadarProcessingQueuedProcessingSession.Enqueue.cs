using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Enqueues an owned radar batch and records the enqueue result in session evidence.
    /// </summary>
    public async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var result = await queue.EnqueueAsync(
            batch,
            ownedSnapshotTime,
            ownedSnapshotAllocatedBytes,
            cancellationToken).ConfigureAwait(false);
        RecordEnqueueResult(result);
        return result;
    }

    /// <summary>
    /// Closes the provider queue to new batches while allowing accepted batches to drain.
    /// </summary>
    public void CompleteAdding() => queue.Close();

    /// <summary>
    /// Faults the session and queue so later accepted batches are skipped after fault.
    /// </summary>
    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        MarkFaulted(message);
    }

    /// <summary>
    /// Drains the queue sequentially and processes each accepted batch.
    /// </summary>
    /// <returns>
    /// A queued session result containing enqueue evidence, processing outcomes,
    /// queue telemetry, and terminal status.
}
