using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPersistentDurableHandlerDeltaTests
{
    [Fact]
    public void HandlerDeltaIdRemainsStableAcrossAdapterBackedRetry()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);
            var batchId = BatchId("handler-batch");

            queue.Accept(batchId, CreateOwnedBatch(1));
            var firstClaim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
            var firstDelta = CreateDelta(firstClaim.Snapshot, sourceId: 0, events: 2);
            queue.Abandon(batchId, "stale handler worker");
            CreateQueue(path).Retry(batchId, "retry handler delta");
            var secondClaim = CreateQueue(path).ClaimNext("worker-b").ClaimedEnvelope!;
            var replayDelta = CreateDelta(secondClaim.Snapshot, sourceId: 0, events: 2);

            Assert.Equal(firstDelta.DeltaId, replayDelta.DeltaId);
            Assert.Equal(firstDelta.DurableBatchId, replayDelta.DurableBatchId);
            Assert.Equal(firstDelta.ProviderSequence, replayDelta.ProviderSequence);
            Assert.Equal(2, secondClaim.Attempt);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EquivalentDuplicateHandlerDeltaDoesNotDoubleCountAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);

            queue.Accept(BatchId("handler-batch"), CreateOwnedBatch(1));
            var snapshot = Assert.Single(CreateQueue(path).CreateSnapshots());
            var delta = CreateDelta(snapshot, sourceId: 0, events: 5);
            var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());

            var first = coordinator.Complete(delta);
            var duplicate = coordinator.Complete(delta);

            Assert.True(first.IsAccepted);
            Assert.True(duplicate.IsDuplicate);
            Assert.Equal(0, duplicate.AppliedDeltaCount);
            Assert.Equal(5, Assert.Single(duplicate.Summary.MergedValues).Int64Value);
            Assert.Equal(1, duplicate.Summary.AppliedDeltaCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConflictingDuplicateHandlerDeltaFailsClosedAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);

            queue.Accept(BatchId("handler-batch"), CreateOwnedBatch(1));
            var snapshot = Assert.Single(CreateQueue(path).CreateSnapshots());
            var first = CreateDelta(snapshot, sourceId: 0, events: 5);
            var conflict = new RadarProcessingHandlerDelta(
                first.HandlerName,
                first.HandlerContractVersion,
                first.ProviderSequence,
                first.DurableBatchId,
                first.EventCount,
                first.SourceCount,
                first.PayloadValueCount,
                first.InputChecksum,
                first.DeltaId,
                [
                    RadarProcessingHandlerDeltaValue.ForInt64(0, "events", 7)
                ]);
            var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());

            coordinator.Complete(first);
            var rejected = coordinator.Complete(conflict);

            Assert.True(rejected.IsRejected);
            Assert.Contains("different payload", rejected.Message, StringComparison.Ordinal);
            Assert.Equal(first.ProviderSequence, rejected.Summary.FirstBlockingSequence);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OutOfOrderHandlerDeltasMergeByRestoredProviderSequence()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = CreateQueue(path);

            queue.Accept(BatchId("first"), CreateOwnedBatch(1));
            queue.Accept(BatchId("second"), CreateOwnedBatch(3));
            var snapshots = CreateQueue(path).CreateSnapshots();
            var first = CreateDelta(snapshots[0], sourceId: 0, events: 2);
            var second = CreateDelta(snapshots[1], sourceId: 0, events: 3);
            var coordinator = new RadarProcessingHandlerDeltaMergeCoordinator(new SummingMerger());

            var later = coordinator.Complete(second);
            var earlier = coordinator.Complete(first);

            Assert.True(later.IsAccepted);
            Assert.Equal(0, later.AppliedDeltaCount);
            Assert.Equal(new RadarProcessingQueuedBatchSequence(0), later.Summary.FirstBlockingSequence);
            Assert.True(earlier.IsAccepted);
            Assert.Equal(2, earlier.AppliedDeltaCount);
            Assert.Equal(5, Assert.Single(earlier.Summary.MergedValues).Int64Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RadarProcessingDurableEnvelopeQueue CreateQueue(
        string path) =>
        new(new RadarProcessingFileDurableEnvelopeStore(path));

    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static RadarProcessingHandlerDelta CreateDelta(
        RadarProcessingDurableEnvelopeSnapshot snapshot,
        int sourceId,
        long events) =>
        RadarProcessingHandlerDelta.Create(
            "analytics",
            "v1",
            snapshot.ProviderSequence,
            snapshot.BatchId,
            snapshot.StreamEventCount,
            sourceCount: 2,
            snapshot.PayloadValueCount,
            inputChecksum: snapshot.RawValueChecksum,
            values:
            [
                RadarProcessingHandlerDeltaValue.ForInt64(sourceId, "events", events)
            ]);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "radarpulse-m026-handler-",
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

    private sealed class SummingMerger : IRadarProcessingHandlerDeltaMerger
    {
        public string HandlerName => "analytics";

        public string HandlerContractVersion => "v1";

        public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
            IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
            RadarProcessingHandlerDelta delta)
        {
            var values = currentValues.ToDictionary(
                static value => (value.SourceId, value.FieldName),
                static value => value.Int64Value);

            foreach (var value in delta.Values)
            {
                var key = (value.SourceId, value.FieldName);
                values[key] = values.GetValueOrDefault(key) + value.Int64Value;
            }

            return values
                .OrderBy(static pair => pair.Key.SourceId)
                .ThenBy(static pair => pair.Key.FieldName, StringComparer.Ordinal)
                .Select(static pair => RadarProcessingHandlerDeltaValue.ForInt64(
                    pair.Key.SourceId,
                    pair.Key.FieldName,
                    pair.Value))
                .ToArray();
        }
    }
}
