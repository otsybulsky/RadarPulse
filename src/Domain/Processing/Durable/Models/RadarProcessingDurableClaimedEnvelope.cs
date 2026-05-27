namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDurableClaimedEnvelope
{
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

    public RadarProcessingDurableEnvelopeSnapshot Snapshot { get; }

    public RadarProcessingQueuedBatch QueuedBatch { get; }

    public RadarProcessingDurableBatchId BatchId => Snapshot.BatchId;

    public RadarProcessingQueuedBatchSequence ProviderSequence => Snapshot.ProviderSequence;

    public int Attempt => Snapshot.Attempt;
}
