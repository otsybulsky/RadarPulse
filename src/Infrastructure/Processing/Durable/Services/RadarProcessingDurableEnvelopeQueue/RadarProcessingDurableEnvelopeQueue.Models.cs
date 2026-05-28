using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableEnvelopeQueue
{
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
