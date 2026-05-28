using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Durable in-memory envelope index with optional persistent snapshot storage.
/// </summary>
/// <remarks>
/// The queue assigns provider sequences, tracks durable lifecycle transitions,
/// and persists current-schema envelope records after every mutation when a
/// persistent store is configured.
/// </remarks>
public sealed partial class RadarProcessingDurableEnvelopeQueue
{
    private readonly object sync = new();
    private readonly Dictionary<RadarProcessingDurableBatchId, EnvelopeEntry> byBatchId = [];
    private readonly SortedDictionary<long, EnvelopeEntry> bySequence = [];
    private readonly IRadarProcessingPersistentDurableEnvelopeStore? persistentStore;
    private RadarProcessingDurableAdapterCompatibilityStatus adapterCompatibilityStatus =
        RadarProcessingDurableAdapterCompatibilityStatus.Compatible;
    private string adapterStorageMessage = string.Empty;
    private long nextSequence;

    /// <summary>
    /// Creates an in-memory durable envelope queue with no persistent adapter.
    /// </summary>
    public RadarProcessingDurableEnvelopeQueue()
    {
    }

    /// <summary>
    /// Creates a durable envelope queue restored from a persistent store.
    /// </summary>
    /// <remarks>
    /// Incompatible or failed loads are rejected immediately so recovery callers
    /// do not process uncertain durable state.
    /// </remarks>
    public RadarProcessingDurableEnvelopeQueue(
        IRadarProcessingPersistentDurableEnvelopeStore persistentStore)
    {
        ArgumentNullException.ThrowIfNull(persistentStore);

        this.persistentStore = persistentStore;
        var load = persistentStore.Load();
        adapterCompatibilityStatus = load.Status;
        adapterStorageMessage = load.Message;
        if (!load.IsCompatible)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(load.Message)
                    ? $"Persistent durable adapter '{persistentStore.StorageIdentity}' could not load compatible state."
                    : load.Message);
        }

        Restore(load.Records);
    }

    /// <summary>
    /// Number of durable envelopes currently tracked by the queue.
    /// </summary>
    public int Count
    {
        get
        {
            lock (sync)
            {
                return byBatchId.Count;
            }
        }
    }

    /// <summary>
    /// Attempts to read an immutable durable envelope snapshot by batch id.
    /// </summary>
    public bool TryGetSnapshot(
        RadarProcessingDurableBatchId batchId,
        out RadarProcessingDurableEnvelopeSnapshot? snapshot)
    {
        EnsureValidBatchId(batchId);

        lock (sync)
        {
            if (byBatchId.TryGetValue(batchId, out var entry))
            {
                snapshot = entry.ToSnapshot();
                return true;
            }

            snapshot = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to read the queued batch material for a durable envelope.
    /// </summary>
    public bool TryGetQueuedBatch(
        RadarProcessingDurableBatchId batchId,
        out RadarProcessingQueuedBatch? queuedBatch)
    {
        EnsureValidBatchId(batchId);

        lock (sync)
        {
            if (byBatchId.TryGetValue(batchId, out var entry))
            {
                queuedBatch = entry.QueuedBatch;
                return true;
            }

            queuedBatch = null;
            return false;
        }
    }

    /// <summary>
    /// Creates ordered snapshots for all durable envelopes.
    /// </summary>
    public IReadOnlyList<RadarProcessingDurableEnvelopeSnapshot> CreateSnapshots()
    {
        lock (sync)
        {
            if (bySequence.Count == 0)
            {
                return Array.Empty<RadarProcessingDurableEnvelopeSnapshot>();
            }

            var snapshots = new RadarProcessingDurableEnvelopeSnapshot[bySequence.Count];
            var index = 0;
            foreach (var entry in bySequence.Values)
            {
                snapshots[index++] = entry.ToSnapshot();
            }

            return Array.AsReadOnly(snapshots);
        }
    }

    /// <summary>
    /// Creates adapter compatibility and queue-state evidence for diagnostics.
    /// </summary>
    public RadarProcessingDurableAdapterSummary CreateAdapterSummary()
    {
        var queueSummary = CreateSummary();
        if (persistentStore is null)
        {
            return new RadarProcessingDurableAdapterSummary(
                "in-memory",
                RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
                "memory://durable-envelope-queue",
                RadarProcessingDurableAdapterCompatibilityStatus.Compatible,
                queueSummary: queueSummary);
        }

        return persistentStore.CreateSummary(
            queueSummary,
            adapterCompatibilityStatus,
            adapterStorageMessage);
    }

    /// <summary>
    /// Accepts an owned batch into durable state if the batch id is new.
    /// </summary>
    /// <returns>
    /// Accepted with the new envelope snapshot, or duplicate with the existing
    /// snapshot when the batch id has already been recorded.
    /// </returns>
    public RadarProcessingDurableQueueOperationResult Accept(
        RadarProcessingDurableBatchId batchId,
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0)
    {
        EnsureValidBatchId(batchId);
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Lifetime != RadarEventBatchLifetime.Owned)
        {
            throw new ArgumentException("Durable envelope queue accepts only owned RadarEventBatch values.", nameof(batch));
        }

        if (ownedSnapshotTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ownedSnapshotTime));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);

        lock (sync)
        {
            if (byBatchId.TryGetValue(batchId, out var existing))
            {
                return RadarProcessingDurableQueueOperationResult.Duplicate(existing.ToSnapshot());
            }

            var sequence = new RadarProcessingQueuedBatchSequence(nextSequence);
            var queuedBatch = new RadarProcessingQueuedBatch(
                sequence,
                batch,
                ownedSnapshotTime,
                ownedSnapshotAllocatedBytes,
                Stopwatch.GetTimestamp());
            var entry = new EnvelopeEntry(
                batchId,
                queuedBatch,
                Stopwatch.GetTimestamp());

            byBatchId.Add(batchId, entry);
            bySequence.Add(sequence.Value, entry);
            nextSequence = checked(nextSequence + 1);
            PersistLocked();

            return RadarProcessingDurableQueueOperationResult.Accepted(entry.ToSnapshot());
        }
    }

    /// <summary>
    /// Claims the lowest-sequence pending envelope for a worker.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult ClaimNext(
        string workerId = "")
    {
        ArgumentNullException.ThrowIfNull(workerId);

        lock (sync)
        {
            foreach (var entry in bySequence.Values)
            {
                if (entry.State != RadarProcessingDurableEnvelopeState.Pending)
                {
                    continue;
                }

                if (entry.Attempt == 0)
                {
                    entry.Attempt = 1;
                }

                entry.State = RadarProcessingDurableEnvelopeState.Claimed;
                entry.WorkerId = workerId;
                entry.Message = string.Empty;
                entry.ClaimedTimestamp = Stopwatch.GetTimestamp();
                PersistLocked();

                var snapshot = entry.ToSnapshot();
                return RadarProcessingDurableQueueOperationResult.Claimed(
                    new RadarProcessingDurableClaimedEnvelope(snapshot, entry.QueuedBatch));
            }

            return RadarProcessingDurableQueueOperationResult.Empty();
        }
    }

    /// <summary>
    /// Marks a claimed envelope as completed before ordered commit.
    /// </summary>
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
    /// </summary>
    public RadarProcessingDurableQueueSummary CreateSummary()
    {
        lock (sync)
        {
            long pending = 0;
            long claimed = 0;
            long completed = 0;
            long committed = 0;
            long failed = 0;
            long poison = 0;
            long abandoned = 0;
            long canceled = 0;
            long released = 0;
            long retry = 0;
            RadarProcessingQueuedBatchSequence? oldestUncommitted = null;
            RadarProcessingDurableBatchId? firstBlockingBatchId = null;
            RadarProcessingQueuedBatchSequence? firstBlockingSequence = null;
            RadarProcessingDurableEnvelopeState? firstBlockingState = null;
            string firstBlockingReason = string.Empty;

            foreach (var entry in bySequence.Values)
            {
                switch (entry.State)
                {
                    case RadarProcessingDurableEnvelopeState.Pending:
                        pending++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Claimed:
                        claimed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Completed:
                        completed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Committed:
                        committed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Failed:
                        failed++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Poison:
                        poison++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Abandoned:
                        abandoned++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Canceled:
                        canceled++;
                        break;
                    case RadarProcessingDurableEnvelopeState.Released:
                        released++;
                        break;
                    default:
                        RadarProcessingDurableEnvelopeSnapshot.EnsureKnownState(entry.State);
                        throw new ArgumentOutOfRangeException(nameof(entry));
                }

                retry += Math.Max(0, entry.Attempt - 1);
                if (entry.State is RadarProcessingDurableEnvelopeState.Committed or
                    RadarProcessingDurableEnvelopeState.Released)
                {
                    continue;
                }

                oldestUncommitted ??= entry.QueuedBatch.Sequence;
                if (!firstBlockingBatchId.HasValue)
                {
                    firstBlockingBatchId = entry.BatchId;
                    firstBlockingSequence = entry.QueuedBatch.Sequence;
                    firstBlockingState = entry.State;
                    firstBlockingReason = CreateBlockingReason(entry);
                }
            }

            return new RadarProcessingDurableQueueSummary(
                byBatchId.Count,
                pending,
                claimed,
                completed,
                committed,
                failed,
                poison,
                abandoned,
                canceled,
                released,
                retry,
                oldestUncommitted,
                firstBlockingBatchId,
                firstBlockingSequence,
                firstBlockingState,
                firstBlockingReason);
        }
    }

    /// <summary>
    /// Cancels all pending, claimed, and completed envelopes.
    /// </summary>
    /// <returns>The number of envelopes transitioned to canceled.</returns>
    public int CancelOpen(
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (sync)
        {
            var canceled = 0;
            foreach (var entry in bySequence.Values)
            {
                if (entry.State is not RadarProcessingDurableEnvelopeState.Pending and
                    not RadarProcessingDurableEnvelopeState.Claimed and
                    not RadarProcessingDurableEnvelopeState.Completed)
                {
                    continue;
                }

                entry.State = RadarProcessingDurableEnvelopeState.Canceled;
                entry.Message = message;
                entry.CompletedTimestamp = Stopwatch.GetTimestamp();
                canceled++;
            }

            if (canceled > 0)
            {
                PersistLocked();
            }

            return canceled;
        }
    }

    /// <summary>
    /// Releases all canceled envelopes after cleanup.
    /// </summary>
    /// <returns>The number of canceled envelopes transitioned to released.</returns>
    public int ReleaseCanceled(
        string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (sync)
        {
            var released = 0;
            foreach (var entry in bySequence.Values)
            {
                if (entry.State != RadarProcessingDurableEnvelopeState.Canceled)
                {
                    continue;
                }

                entry.State = RadarProcessingDurableEnvelopeState.Released;
                entry.Message = message;
                entry.ReleasedTimestamp = Stopwatch.GetTimestamp();
                released++;
            }

            if (released > 0)
            {
                PersistLocked();
            }

            return released;
        }
    }
}
