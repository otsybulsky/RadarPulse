using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingPersistentDurableEnvelopeRecord
{
    public const int CurrentSchemaVersion = 1;

    public RadarProcessingPersistentDurableEnvelopeRecord(
        int schemaVersion,
        string batchId,
        long providerSequence,
        int attempt,
        RadarProcessingDurableEnvelopeState state,
        string workerId,
        string message,
        int streamEventCount,
        int payloadBytes,
        long payloadValueCount,
        long rawValueChecksum,
        long acceptedTimestamp,
        long claimedTimestamp,
        long completedTimestamp,
        long committedTimestamp,
        long releasedTimestamp,
        TimeSpan ownedSnapshotTime,
        long ownedSnapshotAllocatedBytes,
        long enqueuedTimestamp,
        RadarProcessingPersistentRadarEventBatchRecord batch)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        if (string.IsNullOrWhiteSpace(batchId))
        {
            throw new ArgumentException("Persistent durable batch id must not be empty.", nameof(batchId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(providerSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        RadarProcessingDurableEnvelopeSnapshot.EnsureKnownState(state);
        ArgumentNullException.ThrowIfNull(workerId);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegative(streamEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(claimedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(completedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(committedTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(releasedTimestamp);
        if (ownedSnapshotTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ownedSnapshotTime));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(enqueuedTimestamp);
        ArgumentNullException.ThrowIfNull(batch);
        if (streamEventCount != batch.StreamEventCount)
        {
            throw new ArgumentException(
                "Persistent durable envelope stream event count must match the serialized batch.",
                nameof(streamEventCount));
        }

        if (payloadBytes != batch.PayloadBytes)
        {
            throw new ArgumentException(
                "Persistent durable envelope payload byte count must match the serialized batch.",
                nameof(payloadBytes));
        }

        SchemaVersion = schemaVersion;
        BatchId = batchId;
        ProviderSequence = providerSequence;
        Attempt = attempt;
        State = state;
        WorkerId = workerId;
        Message = message;
        StreamEventCount = streamEventCount;
        PayloadBytes = payloadBytes;
        PayloadValueCount = payloadValueCount;
        RawValueChecksum = rawValueChecksum;
        AcceptedTimestamp = acceptedTimestamp;
        ClaimedTimestamp = claimedTimestamp;
        CompletedTimestamp = completedTimestamp;
        CommittedTimestamp = committedTimestamp;
        ReleasedTimestamp = releasedTimestamp;
        OwnedSnapshotTime = ownedSnapshotTime;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
        EnqueuedTimestamp = enqueuedTimestamp;
        Batch = batch;
    }

    public int SchemaVersion { get; }

    public string BatchId { get; }

    public long ProviderSequence { get; }

    public int Attempt { get; }

    public RadarProcessingDurableEnvelopeState State { get; }

    public string WorkerId { get; }

    public string Message { get; }

    public int StreamEventCount { get; }

    public int PayloadBytes { get; }

    public long PayloadValueCount { get; }

    public long RawValueChecksum { get; }

    public long AcceptedTimestamp { get; }

    public long ClaimedTimestamp { get; }

    public long CompletedTimestamp { get; }

    public long CommittedTimestamp { get; }

    public long ReleasedTimestamp { get; }

    public TimeSpan OwnedSnapshotTime { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public long EnqueuedTimestamp { get; }

    public RadarProcessingPersistentRadarEventBatchRecord Batch { get; }

    public bool IsCurrentSchema => SchemaVersion == CurrentSchemaVersion;

    public RadarProcessingDurableEnvelopeSnapshot ToSnapshot() =>
        new(
            new RadarProcessingDurableBatchId(BatchId),
            new RadarProcessingQueuedBatchSequence(ProviderSequence),
            Attempt,
            State,
            WorkerId,
            Message,
            StreamEventCount,
            PayloadBytes,
            PayloadValueCount,
            RawValueChecksum,
            AcceptedTimestamp,
            ClaimedTimestamp,
            CompletedTimestamp,
            CommittedTimestamp,
            ReleasedTimestamp);

    public RadarProcessingQueuedBatch ToQueuedBatch() =>
        new(
            new RadarProcessingQueuedBatchSequence(ProviderSequence),
            Batch.ToBatch(),
            OwnedSnapshotTime,
            OwnedSnapshotAllocatedBytes,
            EnqueuedTimestamp,
            PayloadValueCount,
            RawValueChecksum);

    public static RadarProcessingPersistentDurableEnvelopeRecord From(
        RadarProcessingDurableEnvelopeSnapshot snapshot,
        RadarProcessingQueuedBatch queuedBatch)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(queuedBatch);
        if (snapshot.ProviderSequence != queuedBatch.Sequence)
        {
            throw new ArgumentException(
                "Persistent durable envelope snapshot must match the queued batch sequence.",
                nameof(queuedBatch));
        }

        if (snapshot.StreamEventCount != queuedBatch.StreamEventCount ||
            snapshot.PayloadBytes != queuedBatch.PayloadBytes ||
            snapshot.PayloadValueCount != queuedBatch.PayloadValueCount ||
            snapshot.RawValueChecksum != queuedBatch.RawValueChecksum)
        {
            throw new ArgumentException(
                "Persistent durable envelope snapshot counters must match the queued batch.",
                nameof(queuedBatch));
        }

        return new RadarProcessingPersistentDurableEnvelopeRecord(
            CurrentSchemaVersion,
            snapshot.BatchId.Value,
            snapshot.ProviderSequence.Value,
            snapshot.Attempt,
            snapshot.State,
            snapshot.WorkerId,
            snapshot.Message,
            snapshot.StreamEventCount,
            snapshot.PayloadBytes,
            snapshot.PayloadValueCount,
            snapshot.RawValueChecksum,
            snapshot.AcceptedTimestamp,
            snapshot.ClaimedTimestamp,
            snapshot.CompletedTimestamp,
            snapshot.CommittedTimestamp,
            snapshot.ReleasedTimestamp,
            queuedBatch.OwnedSnapshotTime,
            queuedBatch.OwnedSnapshotAllocatedBytes,
            queuedBatch.EnqueuedTimestamp,
            RadarProcessingPersistentRadarEventBatchRecord.From(queuedBatch.Batch));
    }
}
