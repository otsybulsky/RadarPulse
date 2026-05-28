using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
    public async ValueTask<RadarProcessingQueuedSessionResult> DrainAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var started = Stopwatch.GetTimestamp();
        try
        {
            while (true)
            {
                var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                switch (dequeue.Status)
                {
                    case RadarProcessingOwnedBatchDequeueStatus.Item:
                        await ProcessDequeuedBatchAsync(dequeue.Batch!, cancellationToken).ConfigureAwait(false);
                        break;

                    case RadarProcessingOwnedBatchDequeueStatus.Closed:
                        AddDrainTime(started);
                        return CreateSessionResult(GetTerminalStatus(), GetTerminalMessage());

                    case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                        MarkFaulted(dequeue.Message);
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Faulted, dequeue.Message);

                    case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                        MarkCanceledAndRecordQueued();
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");

                    case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Disposed, "Queued processing queue was disposed.");

                    default:
                        RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                        throw new ArgumentOutOfRangeException(nameof(dequeue));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceledAndRecordQueued();
            AddDrainTime(started);
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");
        }
    }

    /// <summary>
    /// Computes batches concurrently while publishing committed results by provider sequence.
    /// </summary>
    /// <remarks>
    /// Sequential ordered options fall back to <see cref="DrainAsync"/>. On
    /// failure or cancellation, active work is canceled and later queued batches
}
