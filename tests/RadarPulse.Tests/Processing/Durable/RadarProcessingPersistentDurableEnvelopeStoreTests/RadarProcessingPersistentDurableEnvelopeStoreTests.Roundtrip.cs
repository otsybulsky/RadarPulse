using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPersistentDurableEnvelopeStoreTests
{
    [Fact]
    public void PersistentEnvelopeRecordRoundtripsSnapshotAndBatchPayload()
    {
        var batch = CreateBatch();
        var queuedBatch = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(3),
            batch,
            ownedSnapshotTime: TimeSpan.FromMilliseconds(2),
            ownedSnapshotAllocatedBytes: 128,
            enqueuedTimestamp: 11,
            payloadValueCount: 2,
            rawValueChecksum: 3);
        var snapshot = new RadarProcessingDurableEnvelopeSnapshot(
            new RadarProcessingDurableBatchId("batch-a"),
            queuedBatch.Sequence,
            attempt: 2,
            RadarProcessingDurableEnvelopeState.Claimed,
            workerId: "worker-1",
            message: "claimed",
            streamEventCount: queuedBatch.StreamEventCount,
            payloadBytes: queuedBatch.PayloadBytes,
            payloadValueCount: queuedBatch.PayloadValueCount,
            rawValueChecksum: queuedBatch.RawValueChecksum,
            acceptedTimestamp: 11,
            claimedTimestamp: 12,
            completedTimestamp: 0,
            committedTimestamp: 0,
            releasedTimestamp: 0);

        var record = RadarProcessingPersistentDurableEnvelopeRecord.From(snapshot, queuedBatch);
        var restoredSnapshot = record.ToSnapshot();
        var restoredBatch = record.ToQueuedBatch();

        Assert.True(record.IsCurrentSchema);
        Assert.Equal("batch-a", record.BatchId);
        Assert.Equal(3, restoredSnapshot.ProviderSequence.Value);
        Assert.Equal(2, restoredSnapshot.Attempt);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Claimed, restoredSnapshot.State);
        Assert.Equal("worker-1", restoredSnapshot.WorkerId);
        Assert.Equal("claimed", restoredSnapshot.Message);
        Assert.Equal(queuedBatch.StreamEventCount, restoredBatch.StreamEventCount);
        Assert.Equal(queuedBatch.PayloadBytes, restoredBatch.PayloadBytes);
        Assert.Equal(2, restoredBatch.PayloadValueCount);
        Assert.Equal(3, restoredBatch.RawValueChecksum);
        Assert.Equal(batch.Payload.Span.ToArray(), restoredBatch.Batch.Payload.Span.ToArray());
        Assert.Equal(
            CreateMessageTimestampArray(batch),
            CreateMessageTimestampArray(restoredBatch.Batch));
    }
}
