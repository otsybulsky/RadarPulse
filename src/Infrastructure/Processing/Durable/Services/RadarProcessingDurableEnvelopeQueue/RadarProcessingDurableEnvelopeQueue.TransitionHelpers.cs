using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableEnvelopeQueue
{
    private RadarProcessingDurableQueueOperationResult Transition(
        RadarProcessingDurableBatchId batchId,
        RadarProcessingDurableQueueOperationStatus status,
        RadarProcessingDurableEnvelopeState targetState,
        string message,
        Func<EnvelopeEntry, bool> canTransition,
        Action<EnvelopeEntry> applyTimestamp)
    {
        EnsureValidBatchId(batchId);

        lock (sync)
        {
            if (!byBatchId.TryGetValue(batchId, out var entry))
            {
                return RadarProcessingDurableQueueOperationResult.NotFound(
                    $"Durable envelope '{batchId}' was not found.");
            }

            if (!canTransition(entry))
            {
                return RadarProcessingDurableQueueOperationResult.InvalidState(
                    entry.ToSnapshot(),
                    $"Durable envelope '{batchId}' cannot transition from state {entry.State} to {targetState}.");
            }

            entry.State = targetState;
            entry.Message = message;
            applyTimestamp(entry);
            PersistLocked();

            var snapshot = entry.ToSnapshot();
            return status switch
            {
                RadarProcessingDurableQueueOperationStatus.Completed => RadarProcessingDurableQueueOperationResult.Completed(snapshot),
                RadarProcessingDurableQueueOperationStatus.Failed => RadarProcessingDurableQueueOperationResult.Failed(snapshot),
                RadarProcessingDurableQueueOperationStatus.Abandoned => RadarProcessingDurableQueueOperationResult.Abandoned(snapshot),
                RadarProcessingDurableQueueOperationStatus.Poisoned => RadarProcessingDurableQueueOperationResult.Poisoned(snapshot),
                RadarProcessingDurableQueueOperationStatus.Committed => RadarProcessingDurableQueueOperationResult.Committed(snapshot),
                RadarProcessingDurableQueueOperationStatus.Released => RadarProcessingDurableQueueOperationResult.Released(snapshot),
                RadarProcessingDurableQueueOperationStatus.Canceled => RadarProcessingDurableQueueOperationResult.Canceled(snapshot),
                _ => throw new ArgumentOutOfRangeException(nameof(status))
            };
        }
    }

    private static void EnsureValidBatchId(
        RadarProcessingDurableBatchId batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId.Value))
        {
            throw new ArgumentException("Durable batch id must not be empty.", nameof(batchId));
        }
    }

    private static string CreateBlockingReason(
        EnvelopeEntry entry) =>
        entry.State switch
        {
            RadarProcessingDurableEnvelopeState.Pending => "pending envelope has not been claimed",
            RadarProcessingDurableEnvelopeState.Claimed => "claimed envelope has not completed",
            RadarProcessingDurableEnvelopeState.Completed => "completed envelope has not committed",
            RadarProcessingDurableEnvelopeState.Failed => string.IsNullOrWhiteSpace(entry.Message)
                ? "failed envelope blocks ordered commit"
                : entry.Message,
            RadarProcessingDurableEnvelopeState.Poison => string.IsNullOrWhiteSpace(entry.Message)
                ? "poison envelope blocks ordered commit"
                : entry.Message,
            RadarProcessingDurableEnvelopeState.Abandoned => string.IsNullOrWhiteSpace(entry.Message)
                ? "abandoned envelope requires retry or failure policy"
                : entry.Message,
            RadarProcessingDurableEnvelopeState.Canceled => string.IsNullOrWhiteSpace(entry.Message)
                ? "canceled envelope requires cleanup"
                : entry.Message,
            _ => string.Empty
        };

    private void Restore(
        IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        foreach (var record in records.OrderBy(static item => item.ProviderSequence))
        {
            if (!record.IsCurrentSchema)
            {
                throw new InvalidOperationException(
                    $"Persistent durable envelope schema {record.SchemaVersion} is not supported.");
            }

            var batchId = new RadarProcessingDurableBatchId(record.BatchId);
            if (byBatchId.ContainsKey(batchId))
            {
                throw new InvalidOperationException(
                    $"Persistent durable envelope '{record.BatchId}' is duplicated.");
            }

            if (bySequence.ContainsKey(record.ProviderSequence))
            {
                throw new InvalidOperationException(
                    $"Persistent durable provider sequence {record.ProviderSequence} is duplicated.");
            }

            var entry = new EnvelopeEntry(
                batchId,
                record.ToQueuedBatch(),
                record.AcceptedTimestamp)
            {
                Attempt = record.Attempt,
                State = record.State,
                WorkerId = record.WorkerId,
                Message = record.Message,
                ClaimedTimestamp = record.ClaimedTimestamp,
                CompletedTimestamp = record.CompletedTimestamp,
                CommittedTimestamp = record.CommittedTimestamp,
                ReleasedTimestamp = record.ReleasedTimestamp
            };

            byBatchId.Add(batchId, entry);
            bySequence.Add(record.ProviderSequence, entry);
            nextSequence = Math.Max(nextSequence, checked(record.ProviderSequence + 1));
        }
    }

    private void PersistLocked()
    {
        if (persistentStore is null)
        {
            return;
        }

        var records = new RadarProcessingPersistentDurableEnvelopeRecord[bySequence.Count];
        var index = 0;
        foreach (var entry in bySequence.Values)
        {
            records[index++] = RadarProcessingPersistentDurableEnvelopeRecord.From(
                entry.ToSnapshot(),
                entry.QueuedBatch);
        }

        persistentStore.Save(Array.AsReadOnly(records));
        adapterCompatibilityStatus = RadarProcessingDurableAdapterCompatibilityStatus.Compatible;
        adapterStorageMessage = string.Empty;
    }
}
