using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingOwnedBatchQueue
{
    /// <summary>
    /// Completes the writer side while allowing accepted batches to drain.
    /// </summary>
    public void Close()
    {
        lock (sync)
        {
            if (disposed || closed)
            {
                return;
            }

            closed = true;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }
    }

    /// <summary>
    /// Closes the queue and removes buffered batches that have not been dequeued.
    /// </summary>
    /// <returns>
    /// The canceled queued batches so the owning session can record per-sequence
    /// cancellation results and release retained resources.
    /// </returns>
    public IReadOnlyList<RadarProcessingQueuedBatch> CancelQueued()
    {
        lock (sync)
        {
            if (disposed)
            {
                return Array.Empty<RadarProcessingQueuedBatch>();
            }

            closed = true;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }

        var canceled = new List<RadarProcessingQueuedBatch>();
        while (channel.Reader.TryRead(out var batch))
        {
            RemovePending(batch, countDequeued: false);
            canceled.Add(batch);
        }

        return canceled.Count == 0
            ? Array.Empty<RadarProcessingQueuedBatch>()
            : Array.AsReadOnly(canceled.ToArray());
    }

    /// <summary>
    /// Marks the queue as faulted, closes writers, and wakes blocked producers or consumers.
    /// </summary>
    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            faulted = true;
            closed = true;
            faultMessage = message;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }
    }

    /// <summary>
    /// Creates a telemetry snapshot for queue ownership, pressure, and wait-time evidence.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary CreateTelemetrySummary()
    {
        var recordedSummary = telemetryRecorder.CreateSummary();
        lock (sync)
        {
            return counters.CreateSummary(recordedSummary, pendingCount, pendingPayloadBytes);
        }
    }

    /// <summary>
    /// Disposes the queue, closes the channel, and drops any remaining buffered batches.
    /// </summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            closed = true;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }

        while (channel.Reader.TryRead(out var batch))
        {
            RemovePending(batch, countDequeued: false);
        }
    }

}
