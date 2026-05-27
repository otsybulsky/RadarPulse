using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPersistentDurableEnvelopeQueueTests
{
    [Fact]
    public void FileBackedQueueRestoresAcceptedEnvelopesAndDuplicateAccept()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var firstQueue = new RadarProcessingDurableEnvelopeQueue(
                new RadarProcessingFileDurableEnvelopeStore(path));

            firstQueue.Accept(BatchId("batch-a"), CreateOwnedBatch(1));
            firstQueue.Accept(BatchId("batch-b"), CreateOwnedBatch(3));

            var restoredQueue = new RadarProcessingDurableEnvelopeQueue(
                new RadarProcessingFileDurableEnvelopeStore(path));
            var duplicate = restoredQueue.Accept(BatchId("batch-a"), CreateOwnedBatch(9));
            var claim = restoredQueue.ClaimNext("worker-1");
            var summary = restoredQueue.CreateAdapterSummary();

            Assert.Equal(2, restoredQueue.Count);
            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Duplicate, duplicate.Status);
            Assert.Equal(0, duplicate.Snapshot!.ProviderSequence.Value);
            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Claimed, claim.Status);
            Assert.Equal("batch-a", claim.ClaimedEnvelope!.BatchId.Value);
            Assert.Equal(1, claim.ClaimedEnvelope.Attempt);
            Assert.Equal(RadarProcessingFileDurableEnvelopeStore.Kind, summary.AdapterKind);
            Assert.Equal(path, summary.StorageIdentity);
            Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Compatible, summary.CompatibilityStatus);
            Assert.Equal(1, summary.QueueSummary.ClaimedEnvelopeCount);
            Assert.Equal(1, summary.QueueSummary.PendingEnvelopeCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileBackedQueuePersistsTransitionsAndReleasedTerminalState()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = new RadarProcessingDurableEnvelopeQueue(
                new RadarProcessingFileDurableEnvelopeStore(path));
            var batchId = BatchId("batch-a");

            queue.Accept(batchId, CreateOwnedBatch(1));
            queue.ClaimNext("worker-1");
            queue.Complete(batchId, "processed");
            queue.MarkCommitted(batchId);
            queue.MarkReleased(batchId);

            var restoredQueue = new RadarProcessingDurableEnvelopeQueue(
                new RadarProcessingFileDurableEnvelopeStore(path));
            var empty = restoredQueue.ClaimNext("worker-2");
            var snapshot = Assert.Single(restoredQueue.CreateSnapshots());
            var summary = restoredQueue.CreateSummary();

            Assert.Equal(RadarProcessingDurableQueueOperationStatus.Empty, empty.Status);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Released, snapshot.State);
            Assert.Equal(1, summary.ReleasedEnvelopeCount);
            Assert.False(summary.HasBlockingEnvelope);
            Assert.False(summary.HasUncommittedEnvelope);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileBackedQueueFailsClosedForIncompatibleStore()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            File.WriteAllText(
                path,
                "{\"SchemaVersion\":2,\"Records\":[]}");
            var store = new RadarProcessingFileDurableEnvelopeStore(path);

            var load = store.Load();
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new RadarProcessingDurableEnvelopeQueue(store));
            var summary = store.CreateSummary(
                compatibilityStatus: load.Status,
                storageMessage: load.Message);

            Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Incompatible, load.Status);
            Assert.False(load.IsCompatible);
            Assert.Contains("schema 2", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Incompatible, summary.CompatibilityStatus);
            Assert.True(summary.HasStorageMessage);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileBackedQueueReportsCorruptStoreAsFailedLoad()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            File.WriteAllText(path, "{not-json");
            var store = new RadarProcessingFileDurableEnvelopeStore(path);

            var load = store.Load();

            Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Failed, load.Status);
            Assert.False(load.IsCompatible);
            Assert.False(load.HasRecords);
            Assert.Throws<InvalidOperationException>(() =>
                new RadarProcessingDurableEnvelopeQueue(store));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "radarpulse-m026-",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static RadarEventBatch CreateOwnedBatch(
        byte firstPayloadValue) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            SourceUniverseVersion.Initial,
            new[]
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
            },
            new[] { firstPayloadValue, (byte)(firstPayloadValue + 1) });
}
