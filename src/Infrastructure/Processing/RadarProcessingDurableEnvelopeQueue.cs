using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingDurableEnvelopeQueue
{
    private readonly object sync = new();
    private readonly Dictionary<RadarProcessingDurableBatchId, EnvelopeEntry> byBatchId = [];
    private readonly SortedDictionary<long, EnvelopeEntry> bySequence = [];
    private long nextSequence;

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

            return RadarProcessingDurableQueueOperationResult.Accepted(entry.ToSnapshot());
        }
    }

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

                var snapshot = entry.ToSnapshot();
                return RadarProcessingDurableQueueOperationResult.Claimed(
                    new RadarProcessingDurableClaimedEnvelope(snapshot, entry.QueuedBatch));
            }

            return RadarProcessingDurableQueueOperationResult.Empty();
        }
    }

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
            return RadarProcessingDurableQueueOperationResult.Retried(entry.ToSnapshot());
        }
    }

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

    private sealed class EnvelopeEntry
    {
        public EnvelopeEntry(
            RadarProcessingDurableBatchId batchId,
            RadarProcessingQueuedBatch queuedBatch,
            long acceptedTimestamp)
        {
            BatchId = batchId;
            QueuedBatch = queuedBatch;
            AcceptedTimestamp = acceptedTimestamp;
        }

        public RadarProcessingDurableBatchId BatchId { get; }

        public RadarProcessingQueuedBatch QueuedBatch { get; }

        public int Attempt { get; set; }

        public RadarProcessingDurableEnvelopeState State { get; set; } = RadarProcessingDurableEnvelopeState.Pending;

        public string WorkerId { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public long AcceptedTimestamp { get; }

        public long ClaimedTimestamp { get; set; }

        public long CompletedTimestamp { get; set; }

        public long CommittedTimestamp { get; set; }

        public long ReleasedTimestamp { get; set; }

        public RadarProcessingDurableEnvelopeSnapshot ToSnapshot() =>
            new(
                BatchId,
                QueuedBatch.Sequence,
                Attempt,
                State,
                WorkerId,
                Message,
                QueuedBatch.StreamEventCount,
                QueuedBatch.PayloadBytes,
                QueuedBatch.PayloadValueCount,
                QueuedBatch.RawValueChecksum,
                AcceptedTimestamp,
                ClaimedTimestamp,
                CompletedTimestamp,
                CommittedTimestamp,
                ReleasedTimestamp);
    }
}
