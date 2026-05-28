using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingPersistentDurableEnvelopeStoreTests
{
    [Fact]
    public void PersistentEnvelopeContractsRejectInvalidShapes()
    {
        var batch = CreateBatch();
        var batchRecord = RadarProcessingPersistentRadarEventBatchRecord.From(batch);

        Assert.Equal(1, RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPersistentDurableEnvelopeRecord(
                schemaVersion: 0,
                batchId: "batch",
                providerSequence: 0,
                attempt: 0,
                RadarProcessingDurableEnvelopeState.Pending,
                workerId: "",
                message: "",
                streamEventCount: batch.EventCount,
                payloadBytes: batch.PayloadLength,
                payloadValueCount: 2,
                rawValueChecksum: 3,
                acceptedTimestamp: 1,
                claimedTimestamp: 0,
                completedTimestamp: 0,
                committedTimestamp: 0,
                releasedTimestamp: 0,
                ownedSnapshotTime: TimeSpan.Zero,
                ownedSnapshotAllocatedBytes: 0,
                enqueuedTimestamp: 1,
                batchRecord));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingPersistentDurableEnvelopeRecord(
                RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
                batchId: " ",
                providerSequence: 0,
                attempt: 0,
                RadarProcessingDurableEnvelopeState.Pending,
                workerId: "",
                message: "",
                streamEventCount: batch.EventCount,
                payloadBytes: batch.PayloadLength,
                payloadValueCount: 2,
                rawValueChecksum: 3,
                acceptedTimestamp: 1,
                claimedTimestamp: 0,
                completedTimestamp: 0,
                committedTimestamp: 0,
                releasedTimestamp: 0,
                ownedSnapshotTime: TimeSpan.Zero,
                ownedSnapshotAllocatedBytes: 0,
                enqueuedTimestamp: 1,
                batchRecord));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPersistentDurableEnvelopeRecord(
                RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
                batchId: "batch",
                providerSequence: 0,
                attempt: 0,
                (RadarProcessingDurableEnvelopeState)255,
                workerId: "",
                message: "",
                streamEventCount: batch.EventCount,
                payloadBytes: batch.PayloadLength,
                payloadValueCount: 2,
                rawValueChecksum: 3,
                acceptedTimestamp: 1,
                claimedTimestamp: 0,
                completedTimestamp: 0,
                committedTimestamp: 0,
                releasedTimestamp: 0,
                ownedSnapshotTime: TimeSpan.Zero,
                ownedSnapshotAllocatedBytes: 0,
                enqueuedTimestamp: 1,
                batchRecord));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingPersistentDurableEnvelopeRecord(
                RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
                batchId: "batch",
                providerSequence: 0,
                attempt: 0,
                RadarProcessingDurableEnvelopeState.Pending,
                workerId: "",
                message: "",
                streamEventCount: batch.EventCount + 1,
                payloadBytes: batch.PayloadLength,
                payloadValueCount: 2,
                rawValueChecksum: 3,
                acceptedTimestamp: 1,
                claimedTimestamp: 0,
                completedTimestamp: 0,
                committedTimestamp: 0,
                releasedTimestamp: 0,
                ownedSnapshotTime: TimeSpan.Zero,
                ownedSnapshotAllocatedBytes: 0,
                enqueuedTimestamp: 1,
                batchRecord));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingQueuedBatch(
                RadarProcessingQueuedBatchSequence.Initial,
                batch,
                payloadValueCount: 2));
    }
}
