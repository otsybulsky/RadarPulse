using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingBffReadModelStoreTests
{
    [Fact]
    public void StorePublishesAndReturnsLatestRun()
    {
        var store = new RadarProcessingBffReadModelStore();
        var first = CreateRun("run-a", sourceIds: [0]);
        var second = CreateRun("run-b", sourceIds: [1]);

        store.Publish(first);
        store.Publish(second);

        Assert.Equal(2, store.Count);
        Assert.True(store.TryGetLatestRun(out var latest));
        Assert.Equal("run-b", latest!.RunId);
        Assert.True(store.TryGetRun("run-a", out var found));
        Assert.Equal("run-a", found!.RunId);
        Assert.Equal(["run-a", "run-b"], store.ListRuns().Select(static run => run.RunId).ToArray());
    }

    [Fact]
    public void StoreExposesBatchSourceHandlerAndDiagnosticsQueries()
    {
        var store = new RadarProcessingBffReadModelStore();
        var run = CreateRun(
            "run",
            sourceIds: [0, 1],
            readiness: new RadarProcessingDurableRuntimeReadinessSummary(
                new RadarProcessingDurableQueueSummary(
                    acceptedEnvelopeCount: 1,
                    failedEnvelopeCount: 1,
                    oldestUncommittedSequence: new RadarProcessingQueuedBatchSequence(0),
                    firstBlockingBatchId: new RadarProcessingDurableBatchId("blocked"),
                    firstBlockingSequence: new RadarProcessingQueuedBatchSequence(0),
                    firstBlockingState: RadarProcessingDurableEnvelopeState.Failed,
                    firstBlockingReason: "validation failed")));

        store.Publish(run);

        Assert.Single(store.ListBatches("run"));
        Assert.True(store.TryGetBatch("run", providerSequence: 0, out var batch));
        Assert.True(batch!.IsSuccessful);
        Assert.True(store.TryGetSource("run", sourceId: 1, out var source));
        Assert.Equal(1, source!.Identity.AzimuthBucket);
        Assert.True(store.TryGetHandlerOutput("run", sourceId: 1, "events", out var events));
        Assert.Equal(1, events!.Int64Value);
        Assert.True(store.TryGetHandlerOutputContract("run", out var contract));
        Assert.True(contract!.RequiresSequentialFallback);
        Assert.True(store.TryGetDiagnostics("run", out var diagnostics));
        Assert.False(diagnostics!.IsReady);
        Assert.Equal("validation failed", diagnostics.BlockingReason);
    }

    [Fact]
    public void MissingQueriesReturnStableEmptyShapes()
    {
        var store = new RadarProcessingBffReadModelStore();

        Assert.False(store.TryGetLatestRun(out var latest));
        Assert.Null(latest);
        Assert.Empty(store.ListRuns());
        Assert.Empty(store.ListBatches("missing"));
        Assert.Empty(store.ListSources("missing"));
        Assert.False(store.TryGetRun("missing", out _));
        Assert.False(store.TryGetBatch("missing", providerSequence: 0, out _));
        Assert.False(store.TryGetSource("missing", sourceId: 0, out _));
        Assert.False(store.TryGetHandlerOutput("missing", sourceId: 0, "events", out _));
        Assert.False(store.TryGetHandlerOutputContract("missing", out _));
        Assert.False(store.TryGetDiagnostics("missing", out _));
    }

    [Fact]
    public void PublishingSameRunIdRefreshesLatestRun()
    {
        var store = new RadarProcessingBffReadModelStore();
        store.Publish(CreateRun("same", sourceIds: [0]));
        store.Publish(CreateRun("other", sourceIds: [0]));
        store.Publish(CreateRun("same", sourceIds: [1]));

        Assert.Equal(2, store.Count);
        Assert.True(store.TryGetLatestRun(out var latest));
        Assert.Equal("same", latest!.RunId);
        Assert.Equal(["other", "same"], store.ListRuns().Select(static run => run.RunId).ToArray());
    }

    private static RadarProcessingRunReadModel CreateRun(
        string runId,
        int[] sourceIds,
        RadarProcessingDurableRuntimeReadinessSummary? readiness = null)
    {
        var universe = CreateUniverse(sourceCount: 2);
        var handler = new CountingHandler();
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                handlers: new IRadarSourceProcessingHandler[] { handler }));
        var queuedBatch = new RadarProcessingQueuedBatch(
            new RadarProcessingQueuedBatchSequence(0),
            CreateBatch(universe.Version, sourceIds));
        var processingResult = core.Process(queuedBatch.Batch);
        var sessionResult = new RadarProcessingQueuedSessionResult(
            RadarProcessingQueuedSessionStatus.Completed,
            enqueueResults:
            [
                RadarProcessingQueuedBatchEnqueueResult.Accepted(queuedBatch)
            ],
            processingResults:
            [
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    queuedBatch.Sequence,
                    processingResult)
            ]);

        return RadarProcessingRunReadModelBuilder.FromCore(
            runId,
            universe,
            core,
            sessionResult,
            readiness);
    }

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var builder = new RadarEventBatchBuilder(
            initialEventCapacity: sourceIds.Length,
            initialPayloadCapacity: sourceIds.Length);
        for (var i = 0; i < sourceIds.Length; i++)
        {
            builder.AddEvent(
                new RadarStreamIdentity(
                    sourceIds[i],
                    radarOrdinal: 0,
                    momentId: 0,
                    elevationSlot: 0,
                    azimuthBucket: (ushort)sourceIds[i],
                    rangeBand: 0,
                    dictionaryVersion: DictionaryVersion.Initial,
                    sourceUniverseVersion: sourceUniverseVersion),
                volumeTimestampUtcTicks: 90,
                messageTimestampUtcTicks: 100 + i,
                sourceRecord: 1,
                sourceMessage: 1,
                radialSequence: i,
                gateStart: 0,
                gateCount: 1,
                wordSize: RadarStreamWordSize.EightBit,
                scale: 1.0f,
                offset: 0.0f,
                statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
                payload: new byte[] { (byte)(i + 1) });
        }

        return builder.Build();
    }

    private sealed class CountingHandler : IRadarSourceProcessingHandler
    {
        public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
            new(
                "counting",
                int64SlotCount: 1,
                doubleSlotCount: 0,
                new[]
                {
                    new RadarSourceProcessingSnapshotFieldDescriptor(
                        "events",
                        RadarSourceProcessingSnapshotFieldType.Int64,
                        slotIndex: 0)
                });

        public void Process(
            in RadarSourceProcessingHandlerContext context,
            RadarSourceProcessingState state)
        {
            state.AddInt64(slotIndex: 0, value: 1);
        }
    }
}

