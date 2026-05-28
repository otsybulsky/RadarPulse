using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableEnvelopeQueue
{
    public RadarProcessingDurableQueueOperationResult Complete(
        RadarProcessingDurableBatchId batchId,
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            RadarProcessingDurableQueueOperationStatus.Completed,
            RadarProcessingDurableEnvelopeState.Completed,
            message,
            static entry => entry.State == RadarProcessingDurableEnvelopeState.Claimed,
            static entry => entry.CompletedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Marks a claimed or completed envelope as failed or poison.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult Fail(
        RadarProcessingDurableBatchId batchId,
        string message,
        bool poison = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            poison
                ? RadarProcessingDurableQueueOperationStatus.Poisoned
                : RadarProcessingDurableQueueOperationStatus.Failed,
            poison
                ? RadarProcessingDurableEnvelopeState.Poison
                : RadarProcessingDurableEnvelopeState.Failed,
            message,
            static entry => entry.State is RadarProcessingDurableEnvelopeState.Claimed or
                RadarProcessingDurableEnvelopeState.Completed,
            static entry => entry.CompletedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Abandons a claimed envelope so retry policy can decide later handling.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult Abandon(
        RadarProcessingDurableBatchId batchId,
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            RadarProcessingDurableQueueOperationStatus.Abandoned,
            RadarProcessingDurableEnvelopeState.Abandoned,
            message,
            static entry => entry.State == RadarProcessingDurableEnvelopeState.Claimed,
            static entry => entry.CompletedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Moves a failed or abandoned envelope back to pending and increments attempt count.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult Retry(
        RadarProcessingDurableBatchId batchId,
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureValidBatchId(batchId);

        lock (sync)
        {
            if (!byBatchId.TryGetValue(batchId, out var entry))
            {
                return RadarProcessingDurableQueueOperationResult.NotFound(
                    $"Durable envelope '{batchId}' was not found.");
            }

            if (entry.State is not RadarProcessingDurableEnvelopeState.Failed and
                not RadarProcessingDurableEnvelopeState.Abandoned)
            {
                return RadarProcessingDurableQueueOperationResult.InvalidState(
                    entry.ToSnapshot(),
                    $"Durable envelope '{batchId}' cannot retry from state {entry.State}.");
            }

            entry.Attempt = checked(entry.Attempt + 1);
            entry.State = RadarProcessingDurableEnvelopeState.Pending;
            entry.WorkerId = string.Empty;
            entry.Message = message;
            entry.ClaimedTimestamp = 0;
            entry.CompletedTimestamp = 0;
            entry.CommittedTimestamp = 0;
            entry.ReleasedTimestamp = 0;
            PersistLocked();
            return RadarProcessingDurableQueueOperationResult.Retried(entry.ToSnapshot());
        }
    }

    /// <summary>
    /// Marks a failed or abandoned envelope as poison after retry exhaustion.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult Poison(
        RadarProcessingDurableBatchId batchId,
        string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            RadarProcessingDurableQueueOperationStatus.Poisoned,
            RadarProcessingDurableEnvelopeState.Poison,
            message,
            static entry => entry.State is RadarProcessingDurableEnvelopeState.Failed or
                RadarProcessingDurableEnvelopeState.Abandoned,
            static entry => entry.CompletedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Marks a completed envelope as committed by the ordered publish path.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult MarkCommitted(
        RadarProcessingDurableBatchId batchId,
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            RadarProcessingDurableQueueOperationStatus.Committed,
            RadarProcessingDurableEnvelopeState.Committed,
            message,
            static entry => entry.State == RadarProcessingDurableEnvelopeState.Completed,
            static entry => entry.CommittedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Marks an envelope as released after retained resources are no longer needed.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult MarkReleased(
        RadarProcessingDurableBatchId batchId,
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            RadarProcessingDurableQueueOperationStatus.Released,
            RadarProcessingDurableEnvelopeState.Released,
            message,
            static entry => entry.State is RadarProcessingDurableEnvelopeState.Completed or
                RadarProcessingDurableEnvelopeState.Committed or
                RadarProcessingDurableEnvelopeState.Failed or
                RadarProcessingDurableEnvelopeState.Poison or
                RadarProcessingDurableEnvelopeState.Abandoned or
                RadarProcessingDurableEnvelopeState.Canceled,
            static entry => entry.ReleasedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Marks an open envelope as canceled.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult Cancel(
        RadarProcessingDurableBatchId batchId,
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        return Transition(
            batchId,
            RadarProcessingDurableQueueOperationStatus.Canceled,
            RadarProcessingDurableEnvelopeState.Canceled,
            message,
            static entry => entry.State is RadarProcessingDurableEnvelopeState.Pending or
                RadarProcessingDurableEnvelopeState.Claimed or
                RadarProcessingDurableEnvelopeState.Completed,
            static entry => entry.CompletedTimestamp = Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Summarizes durable lifecycle counts and the first blocking uncommitted envelope.
}
