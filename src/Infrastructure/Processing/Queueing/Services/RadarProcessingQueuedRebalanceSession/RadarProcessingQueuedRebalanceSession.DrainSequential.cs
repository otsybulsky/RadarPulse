using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
{
    /// <summary>
    /// Drains the queue sequentially and rebalance-processes each accepted batch.
    /// </summary>
    /// <returns>
    /// A queued session result containing enqueue evidence, processing or
    /// rebalance outcomes, queue telemetry, terminal status, and final topology version.
    /// </returns>
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
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");

                    case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Disposed, "Queued rebalance queue was disposed.");

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
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");
        }
    }
}
