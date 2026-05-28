using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingOwnedBatchQueue
{
    /// <summary>
    /// Dequeues the next accepted batch or reports queue termination state.
    /// </summary>
    /// <returns>
    /// An item result when a batch is available; otherwise a closed, canceled,
    /// faulted, or disposed result with no batch attached.
    /// </returns>
    public async ValueTask<RadarProcessingOwnedBatchDequeueResult> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Disposed);
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var batch = await channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            RecordDequeue(batch, Stopwatch.GetElapsedTime(started));

            if (IsDisposed)
            {
                return new RadarProcessingOwnedBatchDequeueResult(
                    RadarProcessingOwnedBatchDequeueStatus.Disposed);
            }

            return new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Item,
                batch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddDequeueWaitTime(Stopwatch.GetElapsedTime(started));
            return new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Canceled);
        }
        catch (ChannelClosedException)
        {
            var dequeueWaitTime = Stopwatch.GetElapsedTime(started);
            lock (sync)
            {
                counters.AddDequeueWaitTime(dequeueWaitTime);
                if (disposed)
                {
                    return new RadarProcessingOwnedBatchDequeueResult(
                        RadarProcessingOwnedBatchDequeueStatus.Disposed);
                }

                return faulted
                    ? new RadarProcessingOwnedBatchDequeueResult(
                        RadarProcessingOwnedBatchDequeueStatus.Faulted,
                        message: faultMessage)
                    : new RadarProcessingOwnedBatchDequeueResult(
                        RadarProcessingOwnedBatchDequeueStatus.Closed);
            }
        }
    }

    private void RecordDequeue(
        RadarProcessingQueuedBatch batch,
        TimeSpan dequeueWaitTime)
    {
        RemovePending(batch, countDequeued: true, dequeueWaitTime);
        telemetryRecorder.RecordDequeuedBatch(
            batch,
            batch.EnqueuedTimestamp == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(batch.EnqueuedTimestamp),
            PendingCount,
            PendingPayloadBytes,
            dequeueWaitTime);
    }

    private void AddDequeueWaitTime(
        TimeSpan dequeueWaitTime)
    {
        lock (sync)
        {
            counters.AddDequeueWaitTime(dequeueWaitTime);
        }
    }

    private void RemovePending(
        RadarProcessingQueuedBatch batch,
        bool countDequeued,
        TimeSpan dequeueWaitTime = default)
    {
        lock (sync)
        {
            pendingCount--;
            pendingPayloadBytes -= batch.PayloadBytes;
            if (countDequeued)
            {
                counters.RecordDequeued(dequeueWaitTime);
            }

            SignalRetainedByteBudgetChangedUnsafe();
        }
    }

}
