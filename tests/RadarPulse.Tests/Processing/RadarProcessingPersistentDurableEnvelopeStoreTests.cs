using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPersistentDurableEnvelopeStoreTests
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

    [Fact]
    public void PersistentLoadResultAndAdapterSummaryExposeCompatibility()
    {
        var records = new[]
        {
            CreateRecord("batch-a")
        };
        var compatible = RadarProcessingPersistentDurableEnvelopeLoadResult.Compatible(records);
        var empty = RadarProcessingPersistentDurableEnvelopeLoadResult.Empty("store not found");
        var incompatible = RadarProcessingPersistentDurableEnvelopeLoadResult.Incompatible("schema 2 is unsupported");
        var failed = RadarProcessingPersistentDurableEnvelopeLoadResult.Failed("invalid json");
        var summary = new RadarProcessingDurableAdapterSummary(
            adapterKind: "file",
            schemaVersion: RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion,
            storageIdentity: "durable.json",
            compatibilityStatus: incompatible.Status,
            storageMessage: incompatible.Message,
            queueSummary: new RadarProcessingDurableQueueSummary(acceptedEnvelopeCount: 1));

        Assert.True(compatible.IsCompatible);
        Assert.True(compatible.HasRecords);
        Assert.True(empty.IsCompatible);
        Assert.False(empty.HasRecords);
        Assert.False(incompatible.IsCompatible);
        Assert.False(failed.IsCompatible);
        Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Incompatible, summary.CompatibilityStatus);
        Assert.Equal("file", summary.AdapterKind);
        Assert.Equal("durable.json", summary.StorageIdentity);
        Assert.True(summary.HasStorageMessage);
        Assert.Equal(1, summary.QueueSummary.AcceptedEnvelopeCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingDurableAdapterSummary(
                "file",
                1,
                "durable.json",
                (RadarProcessingDurableAdapterCompatibilityStatus)255));
    }

    [Fact]
    public void PersistentStoreContractCanCreateAdapterSummary()
    {
        var store = new StubPersistentDurableEnvelopeStore();
        var summary = store.CreateSummary(
            new RadarProcessingDurableQueueSummary(pendingEnvelopeCount: 1),
            RadarProcessingDurableAdapterCompatibilityStatus.Empty,
            "empty store");

        Assert.Equal("stub", store.AdapterKind);
        Assert.Equal(RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion, store.SchemaVersion);
        Assert.Equal("memory://stub", store.StorageIdentity);
        Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Empty, summary.CompatibilityStatus);
        Assert.Equal(1, summary.QueueSummary.PendingEnvelopeCount);
    }

    private static RadarProcessingPersistentDurableEnvelopeRecord CreateRecord(
        string batchId)
    {
        var batch = CreateBatch();
        var queuedBatch = new RadarProcessingQueuedBatch(
            RadarProcessingQueuedBatchSequence.Initial,
            batch,
            enqueuedTimestamp: 1,
            payloadValueCount: 2,
            rawValueChecksum: 3);
        var snapshot = new RadarProcessingDurableEnvelopeSnapshot(
            new RadarProcessingDurableBatchId(batchId),
            queuedBatch.Sequence,
            attempt: 0,
            RadarProcessingDurableEnvelopeState.Pending,
            streamEventCount: queuedBatch.StreamEventCount,
            payloadBytes: queuedBatch.PayloadBytes,
            payloadValueCount: queuedBatch.PayloadValueCount,
            rawValueChecksum: queuedBatch.RawValueChecksum,
            acceptedTimestamp: 1);

        return RadarProcessingPersistentDurableEnvelopeRecord.From(snapshot, queuedBatch);
    }

    private static RadarEventBatch CreateBatch()
    {
        var events = new[]
        {
            new RadarStreamEvent(
                sourceId: 0,
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 100,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: 1,
                elevationSlot: 0,
                azimuthBucket: 0,
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: 0,
                payloadLength: 1),
            new RadarStreamEvent(
                sourceId: 1,
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 200,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: 2,
                elevationSlot: 0,
                azimuthBucket: 1,
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: 1,
                payloadLength: 1)
        };

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            events,
            new byte[] { 1, 2 });
    }

    private static long[] CreateMessageTimestampArray(
        RadarEventBatch batch)
    {
        var timestamps = new long[batch.Events.Length];
        var events = batch.Events.Span;
        for (var i = 0; i < events.Length; i++)
        {
            timestamps[i] = events[i].MessageTimestampUtcTicks;
        }

        return timestamps;
    }

    private sealed class StubPersistentDurableEnvelopeStore : IRadarProcessingPersistentDurableEnvelopeStore
    {
        public string AdapterKind => "stub";

        public int SchemaVersion => RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion;

        public string StorageIdentity => "memory://stub";

        public RadarProcessingPersistentDurableEnvelopeLoadResult Load() =>
            RadarProcessingPersistentDurableEnvelopeLoadResult.Empty();

        public void Save(
            IReadOnlyList<RadarProcessingPersistentDurableEnvelopeRecord> records)
        {
            ArgumentNullException.ThrowIfNull(records);
        }

        public RadarProcessingDurableAdapterSummary CreateSummary(
            RadarProcessingDurableQueueSummary? queueSummary = null,
            RadarProcessingDurableAdapterCompatibilityStatus compatibilityStatus =
                RadarProcessingDurableAdapterCompatibilityStatus.Compatible,
            string storageMessage = "") =>
            new(
                AdapterKind,
                SchemaVersion,
                StorageIdentity,
                compatibilityStatus,
                storageMessage,
                queueSummary);
    }
}
