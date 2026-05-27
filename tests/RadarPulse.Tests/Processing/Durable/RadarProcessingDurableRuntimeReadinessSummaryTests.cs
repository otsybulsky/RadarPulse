using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingDurableRuntimeReadinessSummaryTests
{
    [Fact]
    public void CompletedQueueSummaryIsReady()
    {
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(
            new RadarProcessingDurableQueueSummary(
                acceptedEnvelopeCount: 2,
                releasedEnvelopeCount: 2));

        Assert.True(readiness.IsReady);
        Assert.False(readiness.HasBlockingEnvelope);
        Assert.False(readiness.HasUncommittedEnvelope);
        Assert.Equal(2, readiness.AcceptedEnvelopeCount);
        Assert.Equal(2, readiness.ReleasedEnvelopeCount);
        Assert.Equal(string.Empty, readiness.BlockingReason);
    }

    [Fact]
    public void BlockingEnvelopeReportsOperatorReason()
    {
        var batchId = new RadarProcessingDurableBatchId("blocked");
        var readiness = new RadarProcessingDurableRuntimeReadinessSummary(
            new RadarProcessingDurableQueueSummary(
                acceptedEnvelopeCount: 2,
                failedEnvelopeCount: 1,
                oldestUncommittedSequence: new RadarProcessingQueuedBatchSequence(1),
                firstBlockingBatchId: batchId,
                firstBlockingSequence: new RadarProcessingQueuedBatchSequence(1),
                firstBlockingState: RadarProcessingDurableEnvelopeState.Failed,
                firstBlockingReason: "validation failed"));

        Assert.False(readiness.IsReady);
        Assert.True(readiness.HasBlockingEnvelope);
        Assert.True(readiness.HasUncommittedEnvelope);
        Assert.Equal(batchId, readiness.FirstBlockingBatchId);
        Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, readiness.FirstBlockingState);
        Assert.Equal("validation failed", readiness.BlockingReason);
    }

    [Fact]
    public void ReleaseFailureAndTerminalPressureBlockReadiness()
    {
        var releaseFailure = new RadarProcessingDurableRuntimeReadinessSummary(
            new RadarProcessingDurableQueueSummary(
                acceptedEnvelopeCount: 1,
                releasedEnvelopeCount: 1),
            releaseFailureCount: 1);
        var retainedPressure = new RadarProcessingDurableRuntimeReadinessSummary(
            new RadarProcessingDurableQueueSummary(
                acceptedEnvelopeCount: 1,
                releasedEnvelopeCount: 1),
            terminalRetainedEnvelopeCount: 1,
            terminalRetainedPayloadBytes: 128);

        Assert.False(releaseFailure.IsReady);
        Assert.True(releaseFailure.HasReleaseFailures);
        Assert.Equal("release failures 1", releaseFailure.BlockingReason);
        Assert.False(retainedPressure.IsReady);
        Assert.True(retainedPressure.HasTerminalRetainedPressure);
        Assert.Equal("terminal retained pressure envelopes 1, bytes 128", retainedPressure.BlockingReason);
    }

    [Fact]
    public async Task ProcessingAndRebalanceResultsExposeReadinessSummary()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var processingQueue = new RadarProcessingDurableEnvelopeQueue();
        await using var processing = new RadarProcessingDurableProcessingSession(
            CreateCore(universe),
            processingQueue);

        processingQueue.Accept(
            new RadarProcessingDurableBatchId("processing"),
            CreateBatch(universe.Version, [0], messageTimestampBase: 100));
        var processingResult = await processing.DrainAsync();

        var rebalanceQueue = new RadarProcessingDurableEnvelopeQueue();
        await using var rebalance = new RadarProcessingDurableRebalanceSession(
            CreateRebalanceSession(universe),
            rebalanceQueue);
        rebalanceQueue.Accept(
            new RadarProcessingDurableBatchId("rebalance"),
            CreateBatch(universe.Version, [0], messageTimestampBase: 200));
        var rebalanceResult = await rebalance.DrainAsync();

        Assert.True(processingResult.ReadinessSummary.IsReady);
        Assert.Equal(1, processingResult.ReadinessSummary.ReleasedEnvelopeCount);
        Assert.True(rebalanceResult.ReadinessSummary.IsReady);
        Assert.Equal(1, rebalanceResult.ReadinessSummary.ReleasedEnvelopeCount);
    }

    [Fact]
    public void PersistentAdapterSummaryFeedsReadinessSummary()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "durable.json");
            var queue = new RadarProcessingDurableEnvelopeQueue(
                new RadarProcessingFileDurableEnvelopeStore(path));
            var batchId = new RadarProcessingDurableBatchId("blocked");

            queue.Accept(batchId, CreateBatch(SourceUniverseVersion.Initial, [0], messageTimestampBase: 100));
            queue.ClaimNext("worker-a");
            queue.Fail(batchId, "adapter-backed validation failed");

            var adapter = queue.CreateAdapterSummary();
            var readiness = new RadarProcessingDurableRuntimeReadinessSummary(adapter.QueueSummary);

            Assert.Equal(RadarProcessingFileDurableEnvelopeStore.Kind, adapter.AdapterKind);
            Assert.Equal(RadarProcessingPersistentDurableEnvelopeRecord.CurrentSchemaVersion, adapter.SchemaVersion);
            Assert.Equal(path, adapter.StorageIdentity);
            Assert.Equal(RadarProcessingDurableAdapterCompatibilityStatus.Compatible, adapter.CompatibilityStatus);
            Assert.False(readiness.IsReady);
            Assert.Equal(batchId, readiness.FirstBlockingBatchId);
            Assert.Equal(RadarProcessingDurableEnvelopeState.Failed, readiness.FirstBlockingState);
            Assert.Equal("adapter-backed validation failed", readiness.BlockingReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: Math.Min(2, universe.SourceCount)));

    private static RadarProcessingRebalanceSession CreateRebalanceSession(
        RadarSourceUniverse universe) =>
        new(
            CreateCore(universe),
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0),
            new RadarProcessingPressureWindow(
                new RadarProcessingPressureWindowOptions(
                    sampleCapacity: 2,
                    minimumSampleCount: 1,
                    coldThreshold: 0.0,
                    warmExitThreshold: 4.0,
                    warmEnterThreshold: 4.5,
                    hotExitThreshold: 4.75,
                    hotEnterThreshold: 5.0,
                    superHotExitThreshold: 9.0,
                    superHotEnterThreshold: 10.0)));

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
            "radarpulse-m026-readiness-",
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
}
