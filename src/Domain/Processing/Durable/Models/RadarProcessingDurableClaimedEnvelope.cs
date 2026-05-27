namespace RadarPulse.Domain.Processing;

/// <summary>
/// Durable envelope claim paired with its retained queued batch payload.
/// </summary>
/// <remarks>
/// The snapshot must be in the claimed state and must match the queued batch
/// provider sequence. This keeps recovery-safe state and retained payload
/// ownership aligned.
/// </remarks>
public sealed class RadarProcessingDurableClaimedEnvelope
{
    /// <summary>
    /// Creates a claimed envelope from a claimed snapshot and matching queued batch.
    /// </summary>
    public RadarProcessingDurableClaimedEnvelope(
        RadarProcessingDurableEnvelopeSnapshot snapshot,
        RadarProcessingQueuedBatch queuedBatch)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(queuedBatch);

        if (snapshot.State != RadarProcessingDurableEnvelopeState.Claimed)
        {
            throw new ArgumentException("Claimed durable envelopes require a claimed snapshot.", nameof(snapshot));
        }

        if (snapshot.ProviderSequence != queuedBatch.Sequence)
        {
            throw new ArgumentException(
                "Claimed durable envelope snapshot must match the queued batch sequence.",
                nameof(queuedBatch));
        }

        Snapshot = snapshot;
        QueuedBatch = queuedBatch;
    }

    /// <summary>
    /// Claimed durable envelope snapshot.
    /// </summary>
    public RadarProcessingDurableEnvelopeSnapshot Snapshot { get; }

    /// <summary>
    /// Retained owned batch payload associated with the claimed envelope.
    /// </summary>
    public RadarProcessingQueuedBatch QueuedBatch { get; }

    /// <summary>
    /// Durable envelope id.
    /// </summary>
    public RadarProcessingDurableBatchId BatchId => Snapshot.BatchId;

    /// <summary>
    /// Provider ordering key.
    /// </summary>
    public RadarProcessingQueuedBatchSequence ProviderSequence => Snapshot.ProviderSequence;

    /// <summary>
    /// Attempt number for this claim.
    /// </summary>
    public int Attempt => Snapshot.Attempt;
}
