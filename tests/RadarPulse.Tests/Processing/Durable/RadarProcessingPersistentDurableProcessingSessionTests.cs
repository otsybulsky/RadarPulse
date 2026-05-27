using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingPersistentDurableProcessingSessionTests
{
    [Fact]
    public async Task CompletedEnvelopeCanCommitAfterAdapterSessionRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            await using var firstSession = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);
            var batchId = BatchId("batch-0");

            queue.Accept(batchId, CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            var claim = queue.ClaimNext("worker-a").ClaimedEnvelope!;
            await firstSession.ProcessClaimedAsync(claim);

            var restoredQueue = CreateQueue(path);
            await using var restoredSession = new RadarProcessingDurableProcessingSession(
                CreateCore(universe),
                restoredQueue);
            var recovered = await restoredSession.RecoverCompletedAsync();
            var published = restoredSession.CommitReady();
            var finalSummary = CreateQueue(path).CreateSummary();

            Assert.Equal(1, recovered);
            Assert.Equal([0L], published.Select(static item => item.Sequence.Value).ToArray());
            Assert.All(published, static item => Assert.True(item.IsSuccessful));
            Assert.Equal(1, finalSummary.ReleasedEnvelopeCount);
            Assert.False(finalSummary.HasBlockingEnvelope);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OutOfOrderWorkerCompletionCommitsInProviderSequenceWithFileBackedAdapter()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 3);
            var queue = CreateQueue(path);
            await using var session = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

            queue.Accept(BatchId("batch-0"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            queue.Accept(BatchId("batch-1"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));
            queue.Accept(BatchId("batch-2"), CreateBatch(universe.Version, [2], messageTimestampBase: 300));

            var first = queue.ClaimNext("worker-a").ClaimedEnvelope!;
            var second = queue.ClaimNext("worker-b").ClaimedEnvelope!;
            var third = queue.ClaimNext("worker-c").ClaimedEnvelope!;

            await session.ProcessClaimedAsync(second);
            Assert.Empty(session.CommitReady());

            await session.ProcessClaimedAsync(first);
            var firstPublished = session.CommitReady();

            await session.ProcessClaimedAsync(third);
            var secondPublished = session.CommitReady();
            var finalSummary = CreateQueue(path).CreateSummary();

            Assert.Equal([0L, 1L], firstPublished.Select(static item => item.Sequence.Value).ToArray());
            Assert.Equal([2L], secondPublished.Select(static item => item.Sequence.Value).ToArray());
            Assert.Equal(3, finalSummary.ReleasedEnvelopeCount);
            Assert.False(finalSummary.HasBlockingEnvelope);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LaterCompletedEnvelopeWaitsBehindEarlierClaimedEnvelopeAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 2);
            var queue = CreateQueue(path);
            await using var firstSession = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

            queue.Accept(BatchId("first"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));
            queue.Accept(BatchId("second"), CreateBatch(universe.Version, [1], messageTimestampBase: 200));

            queue.ClaimNext("worker-a");
            var second = queue.ClaimNext("worker-b").ClaimedEnvelope!;
            await firstSession.ProcessClaimedAsync(second);

            var restoredQueue = CreateQueue(path);
            await using var restoredSession = new RadarProcessingDurableProcessingSession(
                CreateCore(universe),
                restoredQueue);
            var recovered = await restoredSession.RecoverCompletedAsync();
            var published = restoredSession.CommitReady();
            var summary = restoredQueue.CreateSummary();

            Assert.Equal(1, recovered);
            Assert.Empty(published);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Claimed, summary.FirstBlockingState);
            Assert.Equal(0, summary.FirstBlockingSequence!.Value.Value);
            Assert.Equal(1, summary.CompletedEnvelopeCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task EarlierValidationFailureBlocksLaterRecoveredCompletionAfterRestart()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var universe = CreateUniverse(sourceCount: 1);
            var queue = CreateQueue(path);
            await using var firstSession = new RadarProcessingDurableProcessingSession(CreateCore(universe), queue);

            queue.Accept(BatchId("invalid"), CreateInvalidSourceBatch(universe.Version));
            queue.Accept(BatchId("valid"), CreateBatch(universe.Version, [0], messageTimestampBase: 100));

            var invalid = queue.ClaimNext("worker-a").ClaimedEnvelope!;
            var valid = queue.ClaimNext("worker-b").ClaimedEnvelope!;

            await firstSession.ProcessClaimedAsync(valid);
            await firstSession.ProcessClaimedAsync(invalid);

            var restoredQueue = CreateQueue(path);
            await using var restoredSession = new RadarProcessingDurableProcessingSession(
                CreateCore(universe),
                restoredQueue);
            var recovered = await restoredSession.RecoverCompletedAsync();
            var published = restoredSession.CommitReady();
            var summary = restoredQueue.CreateSummary();

            Assert.Equal(1, recovered);
            Assert.Empty(published);
            Assert.Equal(1, summary.FailedEnvelopeCount);
            Assert.Equal(1, summary.CompletedEnvelopeCount);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, summary.FirstBlockingState);
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

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "radarpulse-m026-processing-",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds,
        long messageTimestampBase)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = new RadarStreamEvent(
                sourceIds[i],
                radarOrdinal: 0,
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: messageTimestampBase + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                elevationSlot: 0,
                azimuthBucket: (ushort)sourceIds[i],
                rangeBand: 0,
                momentId: 0,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payloadOffset: i,
                payloadLength: 1);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarEventBatch CreateInvalidSourceBatch(
        SourceUniverseVersion sourceUniverseVersion) =>
        new(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            new[]
            {
                new RadarStreamEvent(
                    sourceId: 99,
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
                    payloadLength: 1)
            },
            new byte[] { 1 });
}
