using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableRebalanceSession
{
    /// <summary>
    /// Applies retry policy to a failed or abandoned durable rebalance envelope.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult RetryOrPoison(
        RadarProcessingDurableBatchId batchId,
        RadarProcessingDurableRetryPolicy? retryPolicy = null,
        string retryMessage = "",
        string poisonMessage = "")
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(retryMessage);
        ArgumentNullException.ThrowIfNull(poisonMessage);

        var effectivePolicy = retryPolicy ?? RadarProcessingDurableRetryPolicy.NoRetry;
        if (!queue.TryGetSnapshot(batchId, out var snapshot))
        {
            return RadarProcessingDurableQueueOperationResult.NotFound(
                $"Durable envelope '{batchId}' was not found.");
        }

        if (effectivePolicy.CanRetry(snapshot!))
        {
            return queue.Retry(batchId, retryMessage);
        }

        if (snapshot!.State is RadarProcessingDurableEnvelopeState.Failed or
            RadarProcessingDurableEnvelopeState.Abandoned)
        {
            return queue.Poison(
                batchId,
                string.IsNullOrWhiteSpace(poisonMessage)
                    ? $"Durable envelope '{batchId}' exhausted retry policy after {snapshot.Attempt} attempt(s)."
                    : poisonMessage);
        }

        return RadarProcessingDurableQueueOperationResult.InvalidState(
            snapshot,
            $"Durable envelope '{batchId}' cannot retry from state {snapshot.State}.");
    }

    /// <summary>
    /// Cancels open durable rebalance work, releases canceled envelopes, and returns a result.
    /// </summary>
    public RadarProcessingDurableRebalanceSessionResult CancelAndCleanup(
        string message = "Durable rebalance was canceled.")
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
    /// Creates a session result from current queue summary, processing results, and topology version.
    /// </summary>
    public RadarProcessingDurableRebalanceSessionResult CreateResult()
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

        return new RadarProcessingDurableRebalanceSessionResult(
            status,
            queue.CreateSummary(),
            processingSnapshot,
            message,
            rebalanceSession.CurrentTopology.Version);
    }
}
