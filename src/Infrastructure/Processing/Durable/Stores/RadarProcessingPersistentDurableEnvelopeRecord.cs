using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Persistable durable envelope record with its queued batch payload.
/// </summary>
/// <remarks>
/// The record stores both durable envelope state and the owned batch material
/// required to rebuild recoverable queue entries. Counters are validated against
/// the serialized batch to prevent recovery from mismatched evidence.
/// </remarks>
public sealed class RadarProcessingPersistentDurableEnvelopeRecord
{
    /// <summary>
    /// Current durable envelope persistence schema version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Creates a persistent durable envelope record from serialized envelope and batch data.
    /// </summary>
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

    /// <summary>
    /// Schema version used to serialize this record.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Durable batch id value.
    /// </summary>
    public string BatchId { get; }

    /// <summary>
    /// Provider sequence assigned by the owned provider queue.
    /// </summary>
    public long ProviderSequence { get; }

    /// <summary>
    /// Claim or processing attempt count for the durable envelope.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Durable lifecycle state captured for recovery.
    /// </summary>
    public RadarProcessingDurableEnvelopeState State { get; }

    /// <summary>
    /// Worker id that last claimed or processed the envelope.
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// Durable lifecycle diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Number of stream events in the queued batch.
    /// </summary>
    public int StreamEventCount { get; }

    /// <summary>
    /// Number of payload bytes in the queued batch.
    /// </summary>
    public int PayloadBytes { get; }

    /// <summary>
    /// Decoded payload value count for deterministic recovery evidence.
    /// </summary>
    public long PayloadValueCount { get; }

    /// <summary>
    /// Raw payload checksum for deterministic recovery evidence.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Timestamp captured when the envelope was accepted.
    /// </summary>
    public long AcceptedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when the envelope was claimed.
    /// </summary>
    public long ClaimedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when processing completed.
    /// </summary>
    public long CompletedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when processing was committed.
    /// </summary>
    public long CommittedTimestamp { get; }

    /// <summary>
    /// Timestamp captured when retained resources were released.
    /// </summary>
    public long ReleasedTimestamp { get; }

    /// <summary>
    /// Time spent creating the owned batch snapshot before enqueue.
    /// </summary>
    public TimeSpan OwnedSnapshotTime { get; }

    /// <summary>
    /// Bytes allocated while creating the owned batch snapshot.
    /// </summary>
    public long OwnedSnapshotAllocatedBytes { get; }

    /// <summary>
    /// Stopwatch timestamp captured when the batch entered the provider queue.
    /// </summary>
    public long EnqueuedTimestamp { get; }

    /// <summary>
    /// Serialized radar event batch needed to rebuild recoverable queued work.
    /// </summary>
    public RadarProcessingPersistentRadarEventBatchRecord Batch { get; }

    /// <summary>
    /// Indicates whether this record uses the schema supported by the current code.
    /// </summary>
    public bool IsCurrentSchema => SchemaVersion == CurrentSchemaVersion;

    /// <summary>
    /// Rehydrates the durable envelope snapshot without the serialized batch payload.
    /// </summary>
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

    /// <summary>
    /// Rehydrates the queued batch represented by this persistent record.
    /// </summary>
    public RadarProcessingQueuedBatch ToQueuedBatch() =>
        new(
            new RadarProcessingQueuedBatchSequence(ProviderSequence),
            Batch.ToBatch(),
            OwnedSnapshotTime,
            OwnedSnapshotAllocatedBytes,
            EnqueuedTimestamp,
            PayloadValueCount,
            RawValueChecksum);

    /// <summary>
    /// Creates a current-schema record from a durable snapshot and matching queued batch.
    /// </summary>
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
