using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Cancels open durable work, releases canceled envelopes, and returns a session result.
    /// </summary>
    public RadarProcessingDurableProcessingSessionResult CancelAndCleanup(
        string message = "Durable processing was canceled.")
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(message);

        MarkCanceled();
        DisposePendingCompletions();
        queue.CancelOpen(message);
        queue.ReleaseCanceled(message);
        return CreateResult();
    }

    /// <summary>
    /// Creates a session result from current queue summary and published processing results.
    /// </summary>
    public RadarProcessingDurableProcessingSessionResult CreateResult()
    {
        RadarProcessingQueuedBatchProcessingResult[] processingSnapshot;
        bool isFaulted;
        bool isCanceled;
        string message;
        lock (sync)
        {
            processingSnapshot = processingResults.ToArray();
            isFaulted = faulted;
            isCanceled = canceled;
            message = faultMessage;
        }

        var status = isCanceled
            ? RadarProcessingQueuedSessionStatus.Canceled
            : isFaulted
                ? RadarProcessingQueuedSessionStatus.Faulted
                : RadarProcessingQueuedSessionStatus.Completed;

        return new RadarProcessingDurableProcessingSessionResult(
            status,
            queue.CreateSummary(),
            processingSnapshot,
            message);
    }
}
