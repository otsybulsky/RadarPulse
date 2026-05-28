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
}
