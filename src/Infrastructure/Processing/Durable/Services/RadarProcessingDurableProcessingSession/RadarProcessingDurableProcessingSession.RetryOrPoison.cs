using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Applies retry policy to a failed or abandoned durable envelope.
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
}
